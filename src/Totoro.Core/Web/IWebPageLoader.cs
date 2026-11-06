namespace Totoro.Core.Web;

/// <summary>
/// Loads web pages in a real browser, for sites that turn other clients away (e.g. behind Cloudflare's bot checks).
/// When a site wants the user to show they're not a bot, the user is asked to, once, in a window of that browser; its
/// cookies then let the loads carry on.
/// </summary>
public interface IWebPageLoader
{
	/// <summary>
	/// Loads <paramref name="url"/> as a page, as if from a link on <paramref name="referrer"/>, and returns what it
	/// holds: a JSON or text response's text, or an HTML page's markup as the server sent it.
	/// </summary>
	/// <exception cref="WebPageException">The site wouldn't serve it, e.g. the user didn't do its check.</exception>
	Task<WebPage> LoadAsync(Uri url, Uri? referrer, CancellationToken ct);

	/// <summary>
	/// Opens <paramref name="url"/> with <paramref name="script"/> run in it before the page's own scripts (in every
	/// frame), and returns the first string the script sends from the page's top frame with
	/// <c>window.chrome.webview.postMessage</c>: e.g. what a site's code computes, caught by wrapping the functions it calls.
	/// Null if nothing is sent within <paramref name="timeout"/> of the page loading.
	/// </summary>
	/// <exception cref="WebPageException">The page couldn't be loaded.</exception>
	/// <exception cref="NotSupportedException">This loader can't run scripts.</exception>
	Task<string?> RunAsync(Uri url, string script, TimeSpan timeout, CancellationToken ct) =>
		throw new NotSupportedException("This page loader can't run scripts in pages.");

	/// <summary>
	/// Whether a load is waiting on the user doing a site's check: time spent then is the user's, so timeouts (e.g. the
	/// plugin tester's) don't count it.
	/// </summary>
	bool IsWaitingForUser => false;
}

/// <param name="Url">Where it ended up, after redirects.</param>
/// <param name="ContentType">e.g. text/html or application/json.</param>
public sealed record WebPage(Uri Url, string ContentType, string Content);

/// <summary>A page couldn't be loaded; the message says why, for the user.</summary>
public sealed class WebPageException(string message) : Exception(message);
