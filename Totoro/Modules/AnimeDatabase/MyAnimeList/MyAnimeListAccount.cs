using System.Web;
using MalApi;
using Microsoft.Extensions.Logging;
using Totoro.Core.Authentication;
using Totoro.Core.Module;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

/// <summary>Signs in to MyAnimeList with its OAuth code flow (with PKCE, handled by MalAuthHelper).</summary>
internal sealed class MyAnimeListAccount(
	MyAnimeListSession session,
	IWebAuthenticator authenticator,
	ILogger<MyAnimeListAccount> logger) : IAccount
{
	// The redirect registered for the app's client id. Nothing listens there; the sign-in window catches it.
	private static readonly Uri s_callback = new("http://localhost:2222/callback");

	public Guid Id => MyAnimeListModule.Id;

	public string Name => "MyAnimeList";

	public bool IsSignedIn => session.IsSignedIn;

	public async Task<AccountUser?> GetUserAsync()
	{
		if (!IsSignedIn)
		{
			return null;
		}

		try
		{
			var client = await session.GetClientAsync();
			var user = await client.User();
			return new AccountUser(user.Name, string.IsNullOrEmpty(user.PictureUri) ? null : user.PictureUri);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't fetch the MyAnimeList user");
			return null;
		}
	}

	public async Task<bool> SignInAsync()
	{
		// Also creates the PKCE verifier that DoAuth sends with the code, so it must come before signing in.
		var start = new Uri(MalAuthHelper.GetAuthUrl(Settings.ClientId));
		if (await authenticator.AuthenticateAsync(start, s_callback) is not { } result)
		{
			return false;
		}

		var code = HttpUtility.ParseQueryString(result.Query)["code"];
		if (string.IsNullOrEmpty(code))
		{
			// E.g. the user declined.
			return false;
		}

		try
		{
			await session.SignInAsync(code);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't finish signing in to MyAnimeList");
			return false;
		}
	}

	public void SignOut()
	{
		session.SignOut();
	}
}
