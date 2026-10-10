using System.Web;
using GraphQL;
using Microsoft.Extensions.Logging;
using Totoro.Core.Authentication;
using Totoro.Core.Module;
using Totoro.Core.Settings;
using TotoroNext.Anime.Anilist;

namespace Totoro.Modules.AnimeDatabase.Anilist;

/// <summary>Signs in to AniList with its implicit OAuth flow, which hands the token back in the redirect's fragment.</summary>
internal sealed class AnilistAccount(
	IModuleSettings<Settings> settings,
	IHttpClientFactory httpClients,
	IWebAuthenticator authenticator,
	ILogger<AnilistAccount> logger) : IAccount
{
	// The redirect registered for the app's client id: AniList's page that would show the token to copy.
	private static readonly Uri s_callback = new("https://anilist.co/api/v2/oauth/pin");

	public Guid Id => AnilistModule.Id;

	public string Name => "AniList";

	public bool IsSignedIn => !string.IsNullOrEmpty(settings.Value.Token);

	public async Task<AccountUser?> GetUserAsync()
	{
		if (!IsSignedIn)
		{
			return null;
		}

		try
		{
			// A new client: one created before signing in has no token.
			var response = await AnilistModule.CreateClient(httpClients).SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithViewer(new UserQueryBuilder()
															   .WithName()
															   .WithAvatar(new UserAvatarQueryBuilder().WithLarge())).Build()
			});

			return response.Data?.Viewer is { Name: { } name } viewer ? new AccountUser(name, viewer.Avatar?.Large) : null;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't fetch the AniList user");
			return null;
		}
	}

	public async Task<bool> SignInAsync()
	{
		var start = new Uri($"https://anilist.co/api/v2/oauth/authorize?client_id={Settings.ClientId}&response_type=token");
		if (await authenticator.AuthenticateAsync(start, s_callback) is not { } result)
		{
			return false;
		}

		var token = HttpUtility.ParseQueryString(result.Fragment.TrimStart('#'))["access_token"];
		if (string.IsNullOrEmpty(token))
		{
			// E.g. the user declined.
			return false;
		}

		settings.Value.Token = token;
		settings.Save();
		return true;
	}

	public void SignOut()
	{
		settings.Value.Token = null;
		settings.Save();
	}
}
