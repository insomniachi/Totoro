// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Media.Vlc;

namespace Totoro.WinUI.Media;

public sealed partial class VlcMediaPlayerElement : UserControl
{
    public VlcMediaPlayerElement()
    {
        InitializeComponent();
    }

    public event EventHandler Initialized;
    public VlcMediaTransportControls TransportControls { get; set; } = new();
    public LibVLC LibVLC { get; private set; }
    public MediaPlayer MediaPlayer { get; private set; }

    private void VideoView_Initialized(object sender, InitializedEventArgs e)
    {
        LibVLC = new LibVLC(enableDebugLogs: true, e.SwapChainOptions);
        MediaPlayer = new MediaPlayer(LibVLC);
        TransportControls.VideoView = VideoView;
        TransportControls.LibVLC = LibVLC;
        TransportControls.MediaPlayer = MediaPlayer;
        TransportControls.Initialize();
        Initialized?.Invoke(this, EventArgs.Empty);
    }
}
