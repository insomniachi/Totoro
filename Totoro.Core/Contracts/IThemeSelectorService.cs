namespace Totoro.Core.Contracts;

public interface IThemeSelectorService
{
    ElementTheme Theme { get; }

    void Initialize();

    void SetTheme(ElementTheme theme);

    void SetRequestedTheme();
}
