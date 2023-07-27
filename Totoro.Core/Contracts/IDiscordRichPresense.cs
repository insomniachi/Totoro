namespace Totoro.Core.Contracts;

public interface IDiscordRichPresense
{
    void SetPresence();
    void UpdateDetails(string details);
    void UpdateState(string state);
    void UpdateTimer(TimeSpan timeSpan);
    void UpdateImage(string url);
    void Clear();
    void ClearTimer();
    void Initialize();
    void SetUrl(string url);
    bool IsInitialized { get; }
}
