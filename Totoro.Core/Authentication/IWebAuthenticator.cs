namespace Totoro.Core.Authentication;

/// <summary>Runs a browser-based sign-in (e.g. OAuth) inside the app.</summary>
public interface IWebAuthenticator
{
	/// <summary>
	/// Opens <paramref name="startUri"/> and lets the user sign in. Completes when the site redirects to an address
	/// starting with <paramref name="callbackUri"/>, returning that full address (with its query and fragment, which carry
	/// the result), or null if the user closed the window first.
	/// </summary>
	Task<Uri?> AuthenticateAsync(Uri startUri, Uri callbackUri);
}
