using CommunityToolkit.WinUI.UI.Controls;
using Flurl.Util;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;


public class AboutAnimePageBase : ReactivePage<AboutAnimeViewModel> { }

public sealed partial class AboutAnimePage : AboutAnimePageBase
{
    public AboutAnimePage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.HasTracking)
                .Subscribe(hasTracking =>
                {
                    if (hasTracking)
                    {
                        EditSymbol.Symbol = Microsoft.UI.Xaml.Controls.Symbol.Edit;
                        EditText.Text = "Update";
                    }
                    else
                    {
                        EditSymbol.Symbol = Microsoft.UI.Xaml.Controls.Symbol.Add;
                        EditText.Text = "Add to list";
                    }
                })
                .DisposeWith(d);
        });
    }

    private void ImageTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if(ViewModel.Anime is null)
        {
            return;
        }

        var url = ViewModel.ListType switch
        {
            ListServiceType.MyAnimeList => $@"https://myanimelist.net/anime/{ViewModel.Anime.Id}/",
            ListServiceType.AniList => $@"https://anilist.co/anime/{ViewModel.Anime.Id}/",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(url));
    }

    private void PlayVideo(Microsoft.UI.Xaml.Controls.ItemsView sender, Microsoft.UI.Xaml.Controls.ItemsViewItemInvokedEventArgs args)
    {
        App.Commands.PlayVideo.Execute(args.InvokedItem);
    }
}