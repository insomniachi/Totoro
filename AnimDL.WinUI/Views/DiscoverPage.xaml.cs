using AnimDL.UI.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AnimDL.WinUI.Views;

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
