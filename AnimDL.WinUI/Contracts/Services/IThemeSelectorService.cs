using System.Threading.Tasks;

using Microsoft.UI.Xaml;

namespace AnimDL.WinUI.Contracts.Services;

public interface IThemeSelectorService
{
    ElementTheme Theme { get; }

    void Initialize();

    void SetTheme(ElementTheme theme);

    void SetRequestedTheme();
}
