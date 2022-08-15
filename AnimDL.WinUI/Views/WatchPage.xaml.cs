using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AnimDL.Core.Models;
using AnimDL.WinUI.Helpers;
using AnimDL.WinUI.ViewModels;
using Microsoft.UI.Xaml.Navigation;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;

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
            .SelectMany(ViewModel.OnVideoPlayerMessageRecieved)
            .Subscribe()
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
            .Subscribe(WebView.NavigateToString)
            .DisposeWith(ViewModel.Garbage);

        // Send message to webview
        this.ObservableForProperty(x => x.ViewModel.VideoPlayerRequestMessage, x => x)
            .Subscribe(WebView.CoreWebView2.PostWebMessageAsJson)
            .DisposeWith(ViewModel.Garbage);
    }

    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.NavigateToString("");
    }

}


public enum WebMessageType
{
    Ready,
    TimeUpdate,
    DurationUpdate,
    Ended,
    CanPlay,
    Play,
    Pause,
    Seeked
}

public class WebMessage
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WebMessageType MessageType { get; set; }
    public string Content { get; set; }
}