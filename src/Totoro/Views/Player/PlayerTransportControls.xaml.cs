using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using Totoro.Core.Modules.Media;
using Totoro.UI.Input;
using Totoro.UI.Player;

namespace Totoro.Views.Player;

/// <summary>
/// The player's controls over the video, like WinUI's MediaTransportControls but with the app's own additions. Knows only
/// <see cref="PlayerViewModel"/>, not how the video is played, so any player window can host it.
/// </summary>
public sealed partial class PlayerTransportControls : UserControl
{
	private bool _isPointerOver;
	private bool _isDraggingSeekBar;
	private bool _isUpdatingSeekBar;

	public PlayerTransportControls()
	{
		TimeConverter = new SecondsConverter(this);
		InitializeComponent();
		// The slider handles the pointer itself; dragging it seeks when let go, not on every step.
		SeekBar.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) =>
		{
			_isDraggingSeekBar = true;
			SeekHoverTip.IsOpen = false;
		}), true);
		SeekBar.AddHandler(PointerMovedEvent, new PointerEventHandler(OnSeekBarPointerMoved), true);
		SeekBar.AddHandler(PointerExitedEvent, new PointerEventHandler((_, _) => SeekHoverTip.IsOpen = false), true);
		SeekBar.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => EndSeekBarDrag()), true);
		SeekBar.AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, _) => EndSeekBarDrag()), true);
	}

	public PlayerViewModel ViewModel { get; private set; } = null!;

	/// <summary>The player's settings, for the keys in the buttons' tooltips.</summary>
	public PlayerSettings Shortcuts { get; private set; } = null!;

	/// <summary>Shows the seek bar's seconds as a time and the section it's in, e.g. "1:23 · Opening".</summary>
	public IValueConverter TimeConverter { get; }

	/// <summary>Being used (the pointer's over it, or the seek bar's being dragged): it shouldn't hide.</summary>
	public bool IsBusy => _isPointerOver || _isDraggingSeekBar;

	/// <summary>Starts showing <paramref name="viewModel"/>: before it's loaded, so its bindings start with it.</summary>
	public void Attach(PlayerViewModel viewModel, PlayerSettings shortcuts)
	{
		ViewModel = viewModel;
		Shortcuts = shortcuts;
		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		ViewModel.EpisodeList.PropertyChanged += OnEpisodeListPropertyChanged;
	}

	public void Detach()
	{
		ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
		ViewModel.EpisodeList.PropertyChanged -= OnEpisodeListPropertyChanged;
	}

	/// <summary>So the keys work straight away.</summary>
	public void FocusPlayPause() => PlayPauseButton.Focus(FocusState.Programmatic);

	// For x:Bind function bindings.

	/// <summary>A button's name with its first shortcut key, e.g. "Mute (M)".</summary>
	public static string WithKey(string name, string keys) => KeyboardShortcuts.Split(keys).FirstOrDefault() is { } key ? $"{name} ({KeyboardShortcuts.Label(key)})" : name;

	public static string PlayPauseName(bool isPlaying, string keys) => WithKey(isPlaying ? "Pause" : "Play", keys);

	// MediaTransportControls' glyphs.
	public static string PlayPauseGlyph(bool isPlaying) => isPlaying ? "" : "";

	public static string MuteName(bool isMuted, string keys) => WithKey(isMuted ? "Unmute" : "Mute", keys);

	public static string VolumeGlyph(bool isMuted, double volume) => isMuted || volume <= 0 ? "" : volume < 34 ? "" : volume < 67 ? "" : "";

	public static string FullScreenName(bool isFullScreen, string keys) => WithKey(isFullScreen ? "Exit full screen" : "Full screen", keys);

	public static string FullScreenGlyph(bool isFullScreen) => isFullScreen ? "" : "";

	public static string VolumeText(double volume) => $"{volume:0}";

	/// <summary>How many others are in the watch-together room, for the people button's badge.</summary>
	public static int OthersInRoom(IReadOnlyList<RoomPerson> people) => people.Count(x => !x.IsYou);

	public static Visibility BadgeVisibility(IReadOnlyList<RoomPerson> people) => OthersInRoom(people) > 0 ? Visibility.Visible : Visibility.Collapsed;

	public static string RoomButtonName(IReadOnlyList<RoomPerson> people) => OthersInRoom(people) switch
	{
		0 => "Watching together: nobody else is in the room yet",
		1 => "Watching together with 1 other",
		var others => $"Watching together with {others} others",
	};

	public static string PersonName(string name, bool isYou) => isYou ? $"{name} (you)" : name;

	public static string PersonPlaying(string playing) => playing.Length > 0 ? playing : "Not playing anything";

	/// <summary>The width of an episode's progress bar on its 128-wide thumbnail (inset 6 either side).</summary>
	public static double EpisodeProgressWidth(double progress) => Math.Max(4, progress * 116);

	/// <summary>The episode controls (previous, next, skipping ahead, the episode list...): not for a short video, e.g. an opening.</summary>
	public static Visibility HiddenForClips(bool isClip) => isClip ? Visibility.Collapsed : Visibility.Visible;

	public static Visibility SkipButtonVisibility(bool isClip, bool isSkipButtonVisible) =>
		isClip || !isSkipButtonVisible ? Visibility.Collapsed : Visibility.Visible;

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(PlayerViewModel.Position) when !_isDraggingSeekBar:
				SetSeekBar(() => SeekBar.Value = ViewModel.Position);
				UpdatePlayed(ViewModel.Position);
				break;
			case nameof(PlayerViewModel.Duration):
				SetSeekBar(() => SeekBar.Maximum = ViewModel.Duration);
				DrawSections();
				break;
			case nameof(PlayerViewModel.WatchedAt) or nameof(PlayerViewModel.IsWatched):
				DrawWatchedMark();
				break;
			case nameof(PlayerViewModel.Chapters):
				DrawSections();
				break;
		}
	}

	private void OnEpisodeListPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PlayerEpisodeListViewModel.IsEpisodeListOpen))
		{
			// Opened or closed from elsewhere (a key, picking an episode): the pop-up follows.
			if (ViewModel.EpisodeList.IsEpisodeListOpen && !EpisodeFlyout.IsOpen)
			{
				ShowAbove(EpisodesButton, EpisodeFlyout);
			}
			else if (!ViewModel.EpisodeList.IsEpisodeListOpen && EpisodeFlyout.IsOpen)
			{
				EpisodeFlyout.Hide();
			}
		}
	}

	private void OnPointerEntered(object sender, PointerRoutedEventArgs e) => _isPointerOver = true;

	private void OnPointerExited(object sender, PointerRoutedEventArgs e) => _isPointerOver = false;

	private void OnEpisodesButtonClick(object sender, RoutedEventArgs e) => ViewModel.EpisodeList.ToggleEpisodeListCommand.Execute(null);

	private void OnRoomButtonClick(object sender, RoutedEventArgs e) => ShowAbove(RoomButton, RoomFlyout);

	/// <summary>Opens <paramref name="flyout"/> above the controls (clear of the seek bar), centred over <paramref name="button"/>.</summary>
	private void ShowAbove(FrameworkElement button, Flyout flyout)
	{
		var at = button.TransformToVisual(Panel).TransformPoint(new Point(button.ActualWidth / 2, 0));
		flyout.ShowAt(Panel, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Top, Position = new Point(at.X, -8) });
	}

	private void OnRoomChatKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Enter)
		{
			e.Handled = true;
			ViewModel.Room.SendRoomChat();
		}
	}

	private void OnEpisodeListOpened(object? sender, object e)
	{
		ViewModel.EpisodeList.IsEpisodeListOpen = true;
		if (ViewModel.EpisodeList.Episodes.FirstOrDefault(x => x.IsCurrent) is { } current)
		{
			EpisodeListView.ScrollIntoView(current, ScrollIntoViewAlignment.Leading);
		}
	}

	private void OnEpisodeListClosed(object? sender, object e) => ViewModel.EpisodeList.IsEpisodeListOpen = false;

	private void OnEpisodeClick(object sender, ItemClickEventArgs e) => ViewModel.EpisodeList.PlayEpisodeCommand.Execute(e.ClickedItem);

	private void OnSeekBarValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdatePlayed(e.NewValue);
		// Seeks when it's moved by the user (by the keyboard, or a click); a drag seeks when let go.
		if (!_isUpdatingSeekBar && !_isDraggingSeekBar)
		{
			_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(e.NewValue));
		}
	}

	private void OnSeekBarPointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (_isDraggingSeekBar || ViewModel is not { Duration: > 0 } viewModel)
		{
			SeekHoverTip.IsOpen = false;
			return;
		}

		var x = e.GetCurrentPoint(SeekBar).Position.X;
		var seconds = SeekBarPainter.ToFraction(x, SeekBar.ActualWidth) * viewModel.Duration;
		SeekHoverTip.Content = TimeConverter.Convert(seconds, typeof(string), null!, "");
		SeekHoverTip.PlacementRect = new Rect(x, 0, 0, SeekBar.ActualHeight);
		SeekHoverTip.IsOpen = true;
	}

	private void EndSeekBarDrag()
	{
		if (_isDraggingSeekBar)
		{
			_isDraggingSeekBar = false;
			_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(SeekBar.Value));
		}
	}

	private void SetSeekBar(Action set)
	{
		_isUpdatingSeekBar = true;
		try
		{
			set();
		}
		finally
		{
			_isUpdatingSeekBar = false;
		}
	}

	private void OnSeekTrackSizeChanged(object sender, SizeChangedEventArgs e) => DrawSections();

	// Drawn on first use: the canvases are the control's own.
	private SeekBarPainter? _painter;
	private SeekBarPainter Painter => _painter ??= new(SeekTrack, TrackSections, PlayedSections, WatchedMark);

	private void DrawSections()
	{
		if (ViewModel is not null && Painter.DrawSections(ViewModel.Duration, ViewModel.Chapters))
		{
			UpdatePlayed(_isDraggingSeekBar ? SeekBar.Value : ViewModel.Position);
			DrawWatchedMark();
		}
	}

	private void DrawWatchedMark() => Painter.DrawWatchedMark(ViewModel.IsWatched, ViewModel.WatchedAt, ViewModel.Duration);

	private void UpdatePlayed(double seconds)
	{
		if (ViewModel is not null)
		{
			Painter.UpdatePlayed(seconds, ViewModel.Duration);
		}
	}

	private void OnTracksMenuOpening(object sender, object e)
	{
		var menu = (MenuFlyout)sender;
		menu.Items.Clear();
		var tracks = ViewModel.GetTracks();
		foreach (var (kind, label) in new[] { (MediaTrackKind.Audio, "Audio"), (MediaTrackKind.Subtitle, "Subtitles") })
		{
			var group = new MenuFlyoutSubItem { Text = label };
			var ofKind = tracks.Where(x => x.Kind == kind).ToList();
			if (ofKind.Count == 0)
			{
				group.Items.Add(new MenuFlyoutItem { Text = "None", IsEnabled = false });
			}

			foreach (var track in ofKind)
			{
				var item = new RadioMenuFlyoutItem { Text = track.Name, IsChecked = track.IsSelected, GroupName = label };
				item.Click += (_, _) => _ = ViewModel.SelectTrackAsync(kind, track.Id);
				group.Items.Add(item);
			}

			menu.Items.Add(group);
		}
	}

	public static Visibility VisibleIfAny(IReadOnlyList<string> items) => items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	private void OnShaderMenuOpening(object sender, object e)
	{
		var menu = (MenuFlyout)sender;
		menu.Items.Clear();
		var current = ViewModel.GetShaderMode();
		foreach (var (name, i) in ViewModel.ShaderModes.Select((x, i) => (x, i)))
		{
			var item = new RadioMenuFlyoutItem { Text = name, IsChecked = i == current, GroupName = "ShaderMode" };
			item.Click += (_, _) => ViewModel.SelectShaderMode(i);
			menu.Items.Add(item);
		}
	}

	private void OnAspectRatioMenuOpening(object sender, object e)
	{
		var menu = (MenuFlyout)sender;
		menu.Items.Clear();
		var current = ViewModel.AspectRatio;
		foreach (var mode in Enum.GetValues<PlayerAspectRatio>())
		{
			var item = new RadioMenuFlyoutItem
			{
				Text = PlayerViewModel.PlayerAspectRatioText(mode),
				IsChecked = current == mode,
				GroupName = "AspectRatio",
			};
			item.Click += (_, _) => ViewModel.SetAspectRatio(mode);
			menu.Items.Add(item);
		}
	}

	private sealed partial class SecondsConverter(PlayerTransportControls controls) : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is not double seconds)
			{
				return "";
			}

			var viewModel = controls.ViewModel;
			var time = PlayerViewModel.Format(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(viewModel.Duration));
			return viewModel.SegmentAt(seconds) is { } segment ? $"{time} · {segment}" : time;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
	}
}
