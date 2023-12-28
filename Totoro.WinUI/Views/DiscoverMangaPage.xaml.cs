using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class DiscoverMangaPageBase : ReactivePage<DiscoverMangaViewModel> { }
public sealed partial class DiscoverMangaPage : DiscoverMangaPageBase
{
    public DiscoverMangaPage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            ProviderSearchBox
            .Events()
            .QuerySubmitted
            .Select(x => x.sender.Text)
            .InvokeCommand(ViewModel.SearchProvider);
        });
    }

    private void ItemsView_ItemInvoked(Microsoft.UI.Xaml.Controls.ItemsView sender, Microsoft.UI.Xaml.Controls.ItemsViewItemInvokedEventArgs args)
    {
        ViewModel.SelectSearchResult.Execute(args.InvokedItem);
    }
}
