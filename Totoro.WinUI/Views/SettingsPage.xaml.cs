using System.Windows.Media.Effects;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Splat;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Views.SettingsSections;

namespace Totoro.WinUI.Views;

public class SettingsPageBase : ReactivePage<SettingsViewModel> { }
public sealed partial class SettingsPage : SettingsPageBase, IEnableLogger
{
    private static readonly NavigationTransitionInfo _fromLeft = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft };
    private static readonly NavigationTransitionInfo _fromRight = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
    private int _depth = 0;

    public SettingsPage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.BreadCrumbBar.State)
                .Subscribe(x =>
                {
                    Navigate(x);
                    _depth = ViewModel.BreadCrumbBar.BreadCrumbs.Count;
                });
        });
    }

    private bool Navigate(string state)
    {
        return state switch
        {
            "Settings" => Navigate<DefaultSection>(),
            "Settings>Media Player" => Navigate<MediaPlayerSection>(),
            "Settings>Scrappers" => Navigate<ScrappersSection>(),
            "Settings>Tracking" => Navigate<TrackingSection>(),
            "Settings>Torrenting" => Navigate<TorrentingSection>(),
            _ => false
        };
    }

    private bool Navigate<T>()
        where T : Page
    {
        var diff = _depth - ViewModel.BreadCrumbBar.BreadCrumbs.Count;
        return NavFrame.Navigate(typeof(T), ViewModel, diff > 0 ? _fromRight : _fromLeft);
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        try
        {
            var items = sender.ItemsSource as ObservableCollection<string>;
            for (int i = items.Count - 1; i >= args.Index + 1; i--)
            {
                items.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
        }
    }
}
