using Microsoft.UI.Xaml.Controls;
using Totoro.Core.ViewModels.Discover;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.Views.DiscoverSections;

public class SearchProviderSectionBase : ReactivePage<SearchProviderViewModel> { }

public sealed partial class SearchProviderSection : SearchProviderSectionBase
{
    public SearchProviderSection()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            ProviderSearchBox
                .Events()
                .QuerySubmitted
                .Select(x => x.sender.Text)
                .InvokeCommand(ViewModel.SearchProvider);

            SearchResultView
                .Events()
                .ItemInvoked
                .Select(x => x.args.InvokedItem)
                .InvokeCommand(ViewModel.SelectSearchResult);
        });
    }
}
