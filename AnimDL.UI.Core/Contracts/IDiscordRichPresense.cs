namespace AnimDL.UI.Core.Contracts;

public interface IDiscordRichPresense
{
    void SetPresence();
    void UpdateDetails(string details);
    void UpdateState(string state);
    void UpdateTimer(TimeSpan timeSpan);
    void Clear();
    void ClearTimer();
    void Initialize();
    bool IsInitialized { get; }
}
