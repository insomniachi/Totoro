using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.LoadingViews;

public sealed partial class SeasonalCardGridLoadingView : UserControl
{
    public List<int> DummyList { get; } = [.. Enumerable.Range(1, 15)];

    public SeasonalCardGridLoadingView()
    {
        InitializeComponent();
    }
}
