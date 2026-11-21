using System.Text.Json;

namespace Totoro.Core.Web;

/// <summary>
/// What a page loaded in a browser is: Cloudflare's bot check ("Just a moment..."), its block, or the page itself (its
/// text for a JSON or text response, which the browser shows as text, or its markup). Shared by the app's page loader
/// and the plugins' tests, so both tell a check from a page alike.
/// </summary>
public static class PageReader
{
	/// <summary>The script to run in the page (e.g. with WebView2's ExecuteScriptAsync); <see cref="Parse"/> reads its result.</summary>
	public const string Script = """
		JSON.stringify({
			url: location.href,
			type: document.contentType,
			title: document.title,
			check: document.title === 'Just a moment...' || !!document.getElementById('challenge-form'),
			blocked: document.title.startsWith('Attention Required!'),
			content: document.contentType === 'text/html' ? document.documentElement.outerHTML : (document.body ? document.body.innerText : '')
		})
		""";

	/// <summary>
	/// Reads what <see cref="Script"/> returned through ExecuteScriptAsync (a JSON string, as JSON again). Null JSON (a page
	/// still loading) reads as a check, so callers wait for it.
	/// </summary>
	public static PageState Parse(string executeScriptResult)
	{
		if (JsonSerializer.Deserialize<string?>(executeScriptResult) is not { } json)
		{
			return new PageState("", "", Check: true, Blocked: false, "");
		}

		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		return new PageState(
			root.GetProperty("url").GetString() ?? "",
			root.GetProperty("type").GetString() ?? "",
			root.GetProperty("check").GetBoolean(),
			root.GetProperty("blocked").GetBoolean(),
			root.GetProperty("content").GetString() ?? "");
	}
}

/// <param name="Check">Whether it's Cloudflare's bot check rather than the page.</param>
/// <param name="Blocked">Whether Cloudflare refused it outright.</param>
public sealed record PageState(string Url, string Type, bool Check, bool Blocked, string Content);
