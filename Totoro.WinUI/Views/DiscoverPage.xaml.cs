using Microsoft.UI.Xaml.Controls;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class DiscoverPageBase : ReactivePage<DiscoverViewModel> { }
public sealed partial class DiscoverPage : DiscoverPageBase
{
    public DiscoverPage()
    {
        InitializeComponent();


        this.WhenActivated(_ =>
        {
            Observable
            .FromEventPattern(Gallery, "Tapped")
            .Select(x => (x.Sender as FlipView).SelectedItem as FeaturedAnime)
            .InvokeCommand(ViewModel.SelectFeaturedAnime)
            .DisposeWith(ViewModel.Garbage);
        });
    }
}
