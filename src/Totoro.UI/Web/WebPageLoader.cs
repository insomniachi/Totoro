using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Totoro.Core.Agent;
using Totoro.Core.Helpers;
using Totoro.Core.Web;

namespace Totoro.UI.Web;

/// <summary>
/// Loads pages in WebView2 (Edge's engine): a hidden one attached to the main window, one page at a time. Cloudflare's
/// automatic bot check passes by itself in it; when Cloudflare wants a click instead ("Verify you are human"), the page
/// is shown to the user in a dialog, in a WebView2 on the same profile, so the cookies it earns let the hidden one in.
/// </summary>
public sealed class WebPageLoader(Func<nint> windowHandle, Func<XamlRoot?> xamlRoot, IMainThread mainThread) : IWebPageLoader
{
	// How long Cloudflare's automatic check gets to pass before the user is asked to do it; and a page to load.
	private static readonly TimeSpan s_automaticCheck = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan s_loadTimeout = TimeSpan.FromSeconds(30);

	// Reads what the page is: Cloudflare's check ("Just a moment..."), its block, or the page itself (its text for a
	// JSON or text response, which the browser shows as text, or its markup).
	private const string ReadScript = """
		JSON.stringify({
			url: location.href,
			type: document.contentType,
			title: document.title,
			check: document.title === 'Just a moment...' || !!document.getElementById('challenge-form'),
			blocked: document.title.startsWith('Attention Required!'),
			content: document.contentType === 'text/html' ? document.documentElement.outerHTML : (document.body ? document.body.innerText : '')
		})
		""";

	private readonly SemaphoreSlim _gate = new(1, 1);
	private CoreWebView2Environment? _environment;
	private CoreWebView2Controller? _controller;

	// The hidden browser, got once and kept: each read of CoreWebView2 may give another wrapper of it, and one with handlers
	// on it that's collected takes them with it, so the browser's next event to them crashed the app (at random, whenever
	// the GC had run: Cloudflare's check, with its reloads, made that likely).
	private CoreWebView2? _browser;

	// The page the hidden browser is opening, and as from where (see AddReferrer).
	private (Uri Url, Uri? Referrer)? _opening;

	// The load waiting for the hidden browser's next page (see NextLoadAsync).
	private PendingLoad? _load;

	public bool IsWaitingForUser { get; private set; }

	public async Task<WebPage> LoadAsync(Uri url, Uri? referrer, CancellationToken ct)
	{
		await _gate.WaitAsync(ct);
		try
		{
			// WebView2 only runs on the UI thread.
			return await mainThread.InvokeAsync(() => LoadOnUiThreadAsync(url, referrer, ct));
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task<WebPage> LoadOnUiThreadAsync(Uri url, Uri? referrer, CancellationToken ct)
	{
		var browser = await EnsureBrowserAsync();
		try
		{
			var page = await NavigateAsync(browser, url, referrer, ct);
			if (page.Check)
			{
				// Cloudflare's automatic check reloads the page when it passes, but by posting its form to it: what
				// answers that isn't the page (an API that only takes GET says "Method Not Allowed"). With the check
				// passed, its cookie gets the page itself.
				page = await WaitForPassAsync(browser, page, s_automaticCheck, ct);
				if (!page.Check)
				{
					page = await NavigateAsync(browser, url, referrer, ct);
				}
			}

			if (page.Check)
			{
				if (!await AskUserAsync(url, referrer, ct))
				{
					throw new WebPageException($"{url.Host} wants you to show you're not a bot first, which wasn't done.");
				}

				page = await NavigateAsync(browser, url, referrer, ct);
				if (page.Check)
				{
					throw new WebPageException($"{url.Host} still wants to check you're not a bot. Try again in a moment.");
				}
			}

			return page.Blocked
				? throw new WebPageException($"{url.Host} refused the request (Cloudflare blocked it).")
				: new WebPage(new Uri(page.Url), page.Type, page.Content);
		}
		finally
		{
			// Stops the page running in the background, e.g. a video player loading the video.
			browser.Navigate("about:blank");
		}
	}

	private async Task<CoreWebView2> EnsureBrowserAsync()
	{
		_environment ??= await CoreWebView2Environment.CreateWithOptionsAsync(null, FileHelper.GetPath("WebView2"), new CoreWebView2EnvironmentOptions());
		if (_browser is null)
		{
			var window = CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)windowHandle());
			_controller = await _environment.CreateCoreWebView2ControllerAsync(window);
			_controller.IsVisible = false;
			_browser = _controller.CoreWebView2;
			_browser.IsMuted = true;
			AddReferrer(_browser, () => _opening);
			FollowNavigations(_browser);
		}

		return _browser;
	}

	/// <summary>Opens the page, as from a link on <paramref name="referrer"/>, once it's loaded (its HTML, not its images).</summary>
	private async Task<PageState> NavigateAsync(CoreWebView2 browser, Uri url, Uri? referrer, CancellationToken ct)
	{
		var loaded = NextLoadAsync(url, s_loadTimeout, ct);
		_opening = (url, referrer);
		// Just this page's request is seen (see AddReferrer), not every other the browser makes (e.g. Cloudflare's check's).
		var filter = referrer is null ? null : url.AbsoluteUri;
		if (filter is not null)
		{
			browser.AddWebResourceRequestedFilter(filter, CoreWebView2WebResourceContext.Document);
		}

		try
		{
			browser.Navigate(url.AbsoluteUri);
			await loaded;
		}
		finally
		{
			if (filter is not null)
			{
				browser.RemoveWebResourceRequestedFilter(filter, CoreWebView2WebResourceContext.Document);
			}
		}

		return await ReadAsync(browser);
	}

	/// <summary>Waits for Cloudflare's check to pass and reload the page; returns the page as it is then.</summary>
	private async Task<PageState> WaitForPassAsync(CoreWebView2 browser, PageState page, TimeSpan timeout, CancellationToken ct)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (page.Check && DateTime.UtcNow < deadline)
		{
			try
			{
				await NextLoadAsync(new Uri(page.Url), deadline - DateTime.UtcNow, ct);
			}
			catch (TimeoutException)
			{
				break;
			}

			page = await ReadAsync(browser);
		}

		return page;
	}

	/// <summary>
	/// When the browser's next page has loaded its HTML; fails if it couldn't be loaded at all. Only navigations that
	/// start from now count: not the about:blank the last load ended on, which may still be finishing.
	/// </summary>
	private async Task NextLoadAsync(Uri url, TimeSpan timeout, CancellationToken ct)
	{
		var load = new PendingLoad(url);
		_load = load;
		try
		{
			await load.Loaded.Task.WaitAsync(timeout, ct);
		}
		finally
		{
			if (_load == load)
			{
				_load = null;
			}
		}
	}

	/// <summary>
	/// Follows the hidden browser's navigations for the load waiting on one (<see cref="NextLoadAsync"/>): subscribed
	/// once, for the browser's life, rather than for each load.
	/// </summary>
	private void FollowNavigations(CoreWebView2 browser)
	{
		browser.NavigationStarting += (_, args) =>
		{
			if (!args.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
			{
				_load?.Navigations.Add(args.NavigationId);
			}
		};
		browser.DOMContentLoaded += (_, args) =>
		{
			if (_load is { } load && load.Navigations.Contains(args.NavigationId))
			{
				load.Loaded.TrySetResult();
			}
		};
		browser.NavigationCompleted += (_, args) =>
		{
			// An HTTP error still loads a page (Cloudflare's check is a 403), which DOMContentLoaded reports; only a
			// failure to connect at all ends here.
			if (_load is { } load && load.Navigations.Contains(args.NavigationId) && !args.IsSuccess && args.HttpStatusCode == 0 &&
				args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
			{
				load.Loaded.TrySetException(new WebPageException($"Couldn't load {load.Url} ({args.WebErrorStatus})."));
			}
		};
	}

	/// <summary>A load waiting for its page: the navigations that may be it (redirects and reloads too).</summary>
	private sealed class PendingLoad(Uri url)
	{
		public Uri Url { get; } = url;
		public HashSet<ulong> Navigations { get; } = [];
		public TaskCompletionSource Loaded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	/// <summary>
	/// Sends the referrer with the page's request, as it goes out: set on the navigation itself (NavigateWithWebResourceRequest),
	/// Chromium drops the navigation. Only for the page (its site), not what it loads; and only for getting it: Cloudflare's
	/// check posts its answer to the page, and given another referrer it turns the answer down (and asks again, and again).
	/// </summary>
	private static void AddReferrer(CoreWebView2 browser, Func<(Uri Url, Uri? Referrer)?> opening)
	{
		// Requests are only seen while a page is loaded with a referrer, and only its own (see NavigateAsync).
		browser.WebResourceRequested += (_, args) =>
		{
			var request = args.Request;
			if (opening() is { Referrer: { } referrer } page && request.Method == "GET" &&
				Uri.TryCreate(request.Uri, UriKind.Absolute, out var requested) && requested == page.Url)
			{
				request.Headers.SetHeader("Referer", referrer.AbsoluteUri);
			}
		};
	}

	private static async Task<PageState> ReadAsync(CoreWebView2 browser)
	{
		// The script returns a JSON string, which ExecuteScriptAsync returns as JSON again.
		var json = JsonSerializer.Deserialize<string>(await browser.ExecuteScriptAsync(ReadScript)) ?? "{}";
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		return new PageState(
			root.GetProperty("url").GetString() ?? "",
			root.GetProperty("type").GetString() ?? "",
			root.GetProperty("check").GetBoolean(),
			root.GetProperty("blocked").GetBoolean(),
			root.GetProperty("content").GetString() ?? "");
	}

	/// <summary>
	/// Shows the page in a dialog for the user to do Cloudflare's check, on the hidden browser's profile; it closes by
	/// itself once the check has passed. Returns whether it did.
	/// </summary>
	private async Task<bool> AskUserAsync(Uri url, Uri? referrer, CancellationToken ct)
	{
		var root = xamlRoot() ?? throw new WebPageException($"{url.Host} wants you to show you're not a bot, but the app's window isn't open.");
		var webView = new WebView2
		{
			Width = Math.Max(320, Math.Min(520, root.Size.Width - 96)),
			Height = Math.Max(300, Math.Min(480, root.Size.Height - 260)),
		};
		var dialog = new ContentDialog
		{
			XamlRoot = root,
			Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
			Title = $"Check for {url.Host}",
			CloseButtonText = "Cancel",
			Content = new StackPanel
			{
				Spacing = 12,
				Children =
				{
					new TextBlock
					{
						Text = $"{url.Host} wants to make sure you're not a bot before it answers. Do its check below; this closes by itself once it's done.",
						TextWrapping = TextWrapping.Wrap,
						MaxWidth = webView.Width,
					},
					webView,
				},
			},
		};
		dialog.Resources["ContentDialogMaxWidth"] = 700.0;

		var passed = false;
		dialog.Opened += async (_, _) =>
		{
			await webView.EnsureCoreWebView2Async(_environment);
			var browser = webView.CoreWebView2;
			browser.DOMContentLoaded += async (_, _) =>
			{
				if (!passed && !(await ReadAsync(browser)).Check)
				{
					passed = true;
					dialog.Hide();
				}
			};
			AddReferrer(browser, () => (url, referrer));
			if (referrer is not null)
			{
				browser.AddWebResourceRequestedFilter(url.AbsoluteUri, CoreWebView2WebResourceContext.Document);
			}
			browser.Navigate(url.AbsoluteUri);
		};

		using (ct.Register(() => dialog.Hide()))
		{
			IsWaitingForUser = true;
			try
			{
				await dialog.ShowAsync();
			}
			finally
			{
				IsWaitingForUser = false;
				webView.Close();
			}
		}

		return passed;
	}

	/// <param name="Check">Whether it's Cloudflare's bot check rather than the page.</param>
	/// <param name="Blocked">Whether Cloudflare refused it outright.</param>
	private sealed record PageState(string Url, string Type, bool Check, bool Blocked, string Content);
}
