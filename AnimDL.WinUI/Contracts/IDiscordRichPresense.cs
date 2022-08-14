using System;
using AnimDL.WinUI.Models;

namespace AnimDL.WinUI.Contracts;

public interface IDiscordRichPresense
{
    void SetPresense(AnimeModel a, int episode, TimeSpan duration);
    void SetPresense(string title, int episode, TimeSpan duration);
    void UpdateDetails(string details);
    void Clear();
    void ClearTimer();
    void Initialize();
    bool IsInitialized { get; }
}
