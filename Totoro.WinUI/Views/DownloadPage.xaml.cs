using Totoro.Core.ViewModels;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.Views;

public class DownloadPageBase : ReactivePage<DownloadViewModel> { }

public sealed partial class DownloadPage : DownloadPageBase
{
    public DownloadPage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            SearchBox
            .Events()
            .SuggestionChosen
            .Select(@event => @event.args.SelectedItem as ShanaProjectCatalogItem)
            .Subscribe(x => ViewModel.SelectedSeries = x)
            .DisposeWith(d);

            DownloadBtn
            .Events()
            .Click
            .Subscribe(_ =>
            {
                if(ViewModel.ShowDownloads)
                {
                    ViewModel.ShowDownloads = false;
                }
                else if(ViewModel.TorrentsSerivce.ActiveDownlaods.Any())
                {
                    ViewModel.ShowDownloads = true;
                }
            })
            .DisposeWith(d);
        });
    }
}
