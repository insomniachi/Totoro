using DiscordRPC;

namespace AnimDL.WinUI.Services;

public class DiscordRichPresense : IDiscordRichPresense
{
    private readonly DiscordRpcClient _client = new("997177919052984622");

    public void Initialize() => _client.Initialize();

    public bool IsInitialized => _client.IsInitialized;

    public void SetPresense(AnimeModel anime, int episode, TimeSpan duration)
    {
        var message = new RichPresence()
            .WithDetails(anime.Title)
            .WithState($"Episode {episode}")
            .WithAssets(new Assets() { LargeImageKey = "icon" })
            .WithTimestamps(Timestamps.FromTimeSpan(duration));

        _client.SetPresence(message);
    }

    public void SetPresense(string title, int episode, TimeSpan duration)
    {
        var message = new RichPresence()
            .WithDetails(title)
            .WithState($"Episode {episode}")
            .WithAssets(new Assets() { LargeImageKey = "icon" })
            .WithTimestamps(Timestamps.FromTimeSpan(duration));

        _client.SetPresence(message);
    }

    public void UpdateDetails(string details) => _client.UpdateDetails(details);

    public void Clear()
    {
        _client.UpdateDetails("Idle");
        _client.UpdateState(string.Empty);
        _client.UpdateLargeAsset(string.Empty);
        ClearTimer();
    }

    public void ClearTimer() => _client.UpdateClearTime();

    public void UpdateElapsed()
    {
        _client.UpdateStartTime();
    }
}
