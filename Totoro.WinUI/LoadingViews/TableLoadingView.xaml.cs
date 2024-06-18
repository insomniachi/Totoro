using Microsoft.UI.Xaml.Controls;


namespace Totoro.WinUI.LoadingViews;

public sealed partial class TableLoadingView : UserControl
{
    public List<int> DummyList { get; } = [.. Enumerable.Range(1, 5)];

    public TableLoadingView()
    {
        InitializeComponent();
    }
}
