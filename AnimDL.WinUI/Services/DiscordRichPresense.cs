using System;
using AnimDL.WinUI.Contracts;
using DiscordRPC;
using MalApi;

namespace AnimDL.WinUI.Services;

public class DiscordRichPresense : IDiscordRichPresense
{
    private readonly DiscordRpcClient _client = new("997177919052984622");

    public void Initialize() => _client.Initialize();

    public bool IsInitialized => _client.IsInitialized;

    public void SetPresense(Anime anime, int episode, TimeSpan duration)
    {
        var message = new RichPresence()
            .WithState("Watching")
            .WithDetails($"{anime.Title} - Episode {episode}")
            .WithAssets(new Assets() { LargeImageKey = "icon" })
            .WithTimestamps(Timestamps.FromTimeSpan(duration));

        _client.SetPresence(message);
    }

    public void SetPresense(string title, int episode, TimeSpan duration)
    {
        var message = new RichPresence()
            .WithState("Watching")
            .WithDetails($"{title} - Episode {episode}")
            .WithAssets(new Assets() { LargeImageKey = "icon" })
            .WithTimestamps(Timestamps.FromTimeSpan(duration));

        _client.SetPresence(message);
    }

    public void UpdateState(string details) => _client.UpdateState(details);

    public void Clear() => _client.ClearPresence();

    public void ClearTimer() => _client.UpdateClearTime();

    public void UpdateElapsed()
    {
        _client.UpdateStartTime();
    }
}
