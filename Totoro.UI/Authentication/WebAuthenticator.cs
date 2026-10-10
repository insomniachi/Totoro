using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Authentication;

namespace Totoro.UI.Authentication;

/// <summary>
/// Shows the sign-in page in a dialog with an embedded browser, and closes it as soon as the site redirects to the
/// callback address. The redirect is caught before it loads, so the callback needn't be served by anything.
/// </summary>
public sealed class WebAuthenticator(Func<XamlRoot?> xamlRoot) : IWebAuthenticator
{
	public async Task<Uri?> AuthenticateAsync(Uri startUri, Uri callbackUri)
	{
		var root = xamlRoot() ?? throw new InvalidOperationException("Sign-in can't be shown before the main window has loaded.");

		// Fits typical sign-in pages, shrunk to fit small windows (leaving room for the dialog's title and button).
		var webView = new WebView2
		{
			Width = Math.Max(320, Math.Min(560, root.Size.Width - 96)),
			Height = Math.Max(320, Math.Min(680, root.Size.Height - 200)),
		};

		var dialog = new ContentDialog
		{
			XamlRoot = root,
			Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
			Title = "Sign in",
			CloseButtonText = "Cancel",
			Content = webView,
		};
		// The default maximum width (548) is narrower than most sign-in pages.
		dialog.Resources["ContentDialogMaxWidth"] = 800.0;
		dialog.Resources["ContentDialogMaxHeight"] = 900.0;

		// Compared without a trailing slash: sites add or drop it (".../callback?code=" vs ".../callback/").
		var callback = callbackUri.AbsoluteUri.TrimEnd('/');
		Uri? result = null;

		void CheckForCallback(string? address)
		{
			if (result is null && address is not null && address.StartsWith(callback, StringComparison.OrdinalIgnoreCase))
			{
				result = new Uri(address);
				dialog.Hide();
			}
		}

		webView.CoreWebView2Initialized += (sender, _) =>
		{
			var core = sender.CoreWebView2;
			core.NavigationStarting += (_, args) =>
			{
				if (args.Uri.StartsWith(callback, StringComparison.OrdinalIgnoreCase))
				{
					args.Cancel = true;
				}

				CheckForCallback(args.Uri);
			};
			// A redirect that only changes the fragment (e.g. AniList's "#access_token=...") doesn't start a navigation.
			core.SourceChanged += (_, _) => CheckForCallback(core.Source);
		};

		webView.Source = startUri;

		try
		{
			await dialog.ShowAsync();
		}
		finally
		{
			webView.Close();
		}

		return result;
	}
}
