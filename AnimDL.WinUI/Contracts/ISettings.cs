using AnimDL.Api;
using Microsoft.UI.Xaml;

namespace AnimDL.WinUI.Contracts;

public interface ISettings
{
    ElementTheme ElementTheme { get; set; }
    bool PreferSubs { get; set; }
    ProviderType DefaultProviderType { get; set; }
    bool UseDiscordRichPresense { get; set; }
}
