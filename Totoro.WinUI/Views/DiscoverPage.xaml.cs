using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class DiscoverPageBase : ReactivePage<DiscoverViewModel> { }
public sealed partial class DiscoverPage : DiscoverPageBase
{
    public DiscoverPage()
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
