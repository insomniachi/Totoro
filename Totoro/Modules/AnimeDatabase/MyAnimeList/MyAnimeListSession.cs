using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.Logging;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

/// <summary>
/// The MyAnimeList client and the user's login. MAL logins expire, so the token is refreshed (and saved) on first use
/// after it has. A singleton, so everything shares one login: signing in or out takes effect everywhere.
/// </summary>
internal sealed class MyAnimeListSession(IModuleSettings<Settings> settings, ILogger<MyAnimeListSession> logger)
{
	private readonly SemaphoreSlim _lock = new(1, 1);
	private IMalClient _client = CreateClient();

	public bool IsSignedIn => settings.Value.Auth is not null;

	public bool IncludeNsfw => settings.Value.IncludeNsfw;

	public int SearchLimit => Math.Clamp((int)settings.Value.SearchLimit, 1, 100);

	/// <summary>
	/// The client, with a current access token if the user is signed in. Without a login it can still read public
	/// data (the client id is enough), but not the user's list.
	/// </summary>
	public async Task<IMalClient> GetClientAsync()
	{
		await _lock.WaitAsync();
		try
		{
			if (settings.Value.Auth is not { } auth)
			{
				return _client;
			}

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

			_client.SetAccessToken(auth.AccessToken);
			return _client;
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>Finishes signing in with the code MAL's sign-in page redirected with.</summary>
	public async Task SignInAsync(string code)
	{
		var auth = await MalAuthHelper.DoAuth(Settings.ClientId, code);

		await _lock.WaitAsync();
		try
		{
			settings.Value.Auth = auth;
			settings.Save();
		}
		finally
		{
			_lock.Release();
		}
	}

	public void SignOut()
	{
		_lock.Wait();
		try
		{
			settings.Value.Auth = null;
			settings.Save();
			// The client keeps the token it was given; start over with one that has none.
			_client = CreateClient();
		}
		finally
		{
			_lock.Release();
		}
	}

	private static MalClient CreateClient()
	{
		var client = new MalClient();
		client.SetClientId(Settings.ClientId);
		return client;
	}
}
