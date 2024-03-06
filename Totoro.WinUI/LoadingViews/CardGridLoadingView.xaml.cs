using Microsoft.UI.Xaml.Controls;


namespace Totoro.WinUI.LoadingViews;

public sealed partial class CardGridLoadingView : UserControl
{
    public List<int> DummyList { get; } = [.. Enumerable.Range(1, 15)];

    public GridViewSettings GridSettings { get; } = App.GetService<ISettings>().UserListGridViewSettings;

    public CardGridLoadingView()
    {
        InitializeComponent();
    }
}
