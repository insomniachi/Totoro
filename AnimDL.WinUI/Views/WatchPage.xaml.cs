using Microsoft.UI.Xaml.Navigation;
using ReactiveMarbles.ObservableEvents;

namespace AnimDL.WinUI.Views;

public class WatchPageBase : ReactivePage<WatchViewModel> { }

public sealed partial class WatchPage : WatchPageBase
{
    public WatchPage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            SearchBox
            .Events()
            .SuggestionChosen
            .Select(@event => @event.args.SelectedItem as SearchResult)
            .InvokeCommand(ViewModel.SearchResultPicked)
            .DisposeWith(ViewModel.Garbage);

            WebView
            .Events()
            .WebMessageReceived
            .Select(@event => JsonSerializer.Deserialize<WebMessage>(@event.args.WebMessageAsJson))
            .Subscribe(@event => MessageBus.Current.SendMessage(@event))
            .DisposeWith(ViewModel.Garbage);

            WebView
            .Events()
            .NavigationCompleted
            .Where(@event => @event.args.IsSuccess)
            .SelectMany(@event => @event.sender.ExecuteScriptAsync("document.querySelector('body').style.overflow='hidden'"))
            .Subscribe()
            .DisposeWith(ViewModel.Garbage);

        });
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();

        // Load video html
        this.ObservableForProperty(x => x.ViewModel.Url, x => x)
            .Select(VideoJsHelper.GetPlayerHtml)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(WebView.NavigateToString)
            .DisposeWith(ViewModel.Garbage);

        // Send message to webview
        this.ObservableForProperty(x => x.ViewModel.VideoPlayerRequestMessage, x => x)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(WebView.CoreWebView2.PostWebMessageAsJson)
            .DisposeWith(ViewModel.Garbage);
    }

    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.NavigateToString("");
    }

}
