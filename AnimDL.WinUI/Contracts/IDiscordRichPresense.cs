using System;
using MalApi;

namespace AnimDL.WinUI.Contracts;

public interface IDiscordRichPresense
{
    void SetPresense(Anime a, int episode, TimeSpan duration);
    void SetPresense(string title, int episode, TimeSpan duration);
    void UpdateDetails(string details);
    void Clear();
    void ClearTimer();
    void Initialize();
    bool IsInitialized { get; }
}
