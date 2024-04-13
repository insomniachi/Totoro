using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using LibVLCSharp.Shared;
using ReactiveUI;
using Totoro.Core.ViewModels;

namespace Totoro.Avalonia.Views;

public partial class WatchView : ReactiveUserControl<WatchViewModelTest>
{
    public WatchView()
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        ME.MediaPlayer = ViewModel!.MediaPlayer;
        ViewModel!.Play();;
    }
}

public class WatchViewModelTest : NavigatableViewModel
{
    private readonly LibVLC _libVlc = new LibVLC();
    public WatchViewModelTest()
    {
        MediaPlayer = new MediaPlayer(_libVlc);
    }

    public void Play()
    {
        using var media = new Media(_libVlc, new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"));
        MediaPlayer.Play(media);
    }
    
    public MediaPlayer MediaPlayer { get; }
}