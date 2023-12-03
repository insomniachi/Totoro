// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Media.Vlc;

namespace Totoro.WinUI.Media;

public sealed partial class VlcMediaPlayerElement : UserControl
{
    private LibVLC _libVlc;
    private MediaPlayer _player;

    public VlcMediaPlayerElement()
    {
        InitializeComponent();
    }

    public VlcMediaTransportControls TransportControls
    {
        get { return (VlcMediaTransportControls)GetValue(TransportControlsProperty); }
        set { SetValue(TransportControlsProperty, value); }
    }

    public static readonly DependencyProperty TransportControlsProperty =
        DependencyProperty.Register("TransportControls", typeof(VlcMediaTransportControls), typeof(VlcMediaPlayerElement), new PropertyMetadata(null));


    private LibVLCMediaPlayerWrapper _mediaPlayer;
    internal LibVLCMediaPlayerWrapper MediaPlayer
    {
        get => _mediaPlayer;
        set
        {
            _mediaPlayer = value;
            TransportControls = _mediaPlayer?.TransportControls as VlcMediaTransportControls;
            TransportControls?.SetDynamicSkipButton(DynamicSkipIntroButton);
            if (_libVlc is null || _mediaPlayer is null)
            {
                return;
            }
            _mediaPlayer.Initialize(_libVlc, _player, VideoView);
        }
    }

    private void VideoView_Initialized(object sender, InitializedEventArgs e)
    {
        _libVlc = new LibVLC(e.SwapChainOptions);
        _player = new MediaPlayer(_libVlc);

        if (MediaPlayer is null)
        {
            return;
        }

        MediaPlayer.Initialize(_libVlc, _player, VideoView);
    }
}
