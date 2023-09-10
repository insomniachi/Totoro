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
}
