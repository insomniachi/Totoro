using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.Logging;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

/// <summary>
/// The MyAnimeList client, signed in with the stored login when there is one. MAL logins expire, so the token is
/// refreshed (and saved) on first use after it has.
/// </summary>
internal sealed class MyAnimeListSession(IMalClient client, IModuleSettings<Settings> settings, ILogger<MyAnimeListSession> logger)
{
	private readonly SemaphoreSlim _lock = new(1, 1);

	public bool IsSignedIn => settings.Value.Auth is not null;

	public bool IncludeNsfw => settings.Value.IncludeNsfw;

	public int SearchLimit => Math.Clamp((int)settings.Value.SearchLimit, 1, 100);

	/// <summary>
	/// The client, with a current access token if the user is signed in. Without a login it can still read public
	/// data (the client id is enough), but not the user's list.
	/// </summary>
	public async Task<IMalClient> GetClientAsync()
	{
		if (settings.Value.Auth is null)
		{
			return client;
		}

		await _lock.WaitAsync();
		try
		{
			var auth = settings.Value.Auth;
			if (auth.IsExpired)
			{
				try
				{
					auth = await MalAuthHelper.RefreshToken(Settings.ClientId, auth.RefreshToken);
					settings.Value.Auth = auth;
					settings.Save();
				}
				catch (Exception ex)
				{
					// Carry on with the old token; requests that need the login will fail and say so.
					logger.LogWarning(ex, "Couldn't refresh the MyAnimeList login");
				}
			}

			client.SetAccessToken(auth.AccessToken);
			return client;
		}
		finally
		{
			_lock.Release();
		}
	}
}
