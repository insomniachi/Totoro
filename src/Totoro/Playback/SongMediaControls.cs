using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Totoro.Core.Services;
using Windows.Media;
using Windows.Storage.Streams;
using WinUIEx.Messaging;

namespace Totoro.Playback;

/// <summary>
/// The song playing, controlled from outside the app: in Windows' media controls (the media keys, and the flyout by the
/// volume), with its title, singer and cover, and as previous, play/pause and next buttons under the taskbar button's
/// thumbnail. So songs can be controlled with the app minimised. Next and previous work in a playlist only. Created on the
/// UI thread, once the main window is open.
/// </summary>
internal sealed partial class SongMediaControls : IRecipient<SongPlayerChangedMessage>, IRecipient<SongQueueChangedMessage>
{
	private const uint PreviousButton = 1, PlayPauseButton = 2, NextButton = 3;

	private readonly ISongPlayer _player;
	private readonly SongPlaylistService _playlists;
	private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
	private readonly SystemMediaTransportControls? _controls;
	// What the controls show, to change it (and fetch the cover) only when it changes, not on every pause.
	private (AnimeSong? Song, PlaylistSong? Item) _shown;

	private readonly IntPtr _window;
	private readonly ITaskbarList3? _taskbar;
	// Kept: it stops listening to the window when it's collected.
	private readonly WindowMessageMonitor? _messages;
	// Sent when the taskbar button's made (again, if Explorer restarts), which the buttons are added to.
	private readonly uint _taskbarButtonCreated = RegisterWindowMessage("TaskbarButtonCreated");
	private bool _buttonsAdded;
	private readonly Dictionary<string, IntPtr> _icons = [];

	public SongMediaControls(ISongPlayer player, SongPlaylistService playlists, IntPtr window, ILogger<SongMediaControls> logger)
	{
		_player = player;
		_playlists = playlists;
		_window = window;
		// Each on its own: without one the songs still play, they just can't be controlled from there.
		try
		{
			_controls = SystemMediaTransportControlsInterop.GetForWindow(window);
			_controls.IsPlayEnabled = _controls.IsPauseEnabled = _controls.IsStopEnabled = true;
			_controls.ButtonPressed += OnButtonPressed;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Windows' media controls aren't available");
			_controls = null;
		}

		try
		{
			_taskbar = (ITaskbarList3)new TaskbarList();
			_taskbar.HrInit();
			_messages = new WindowMessageMonitor(window);
			_messages.WindowMessageReceived += OnWindowMessage;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "The taskbar's thumbnail buttons aren't available");
			_taskbar = null;
		}

		WeakReferenceMessenger.Default.RegisterAll(this);
		Update();
	}

	public void Receive(SongPlayerChangedMessage message) => Update();

	public void Receive(SongQueueChangedMessage message) => Update();

	private void Update()
	{
		var song = _player.Current;
		var item = _playlists.Current is { } current && ReferenceEquals(current.Song, song) ? current : null;
		UpdateButtons(song is not null, item is not null);
		if (_controls is null)
		{
			return;
		}

		_controls.IsEnabled = song is not null;
		_controls.PlaybackStatus = song is null ? MediaPlaybackStatus.Closed : _player.IsPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
		_controls.IsNextEnabled = _controls.IsPreviousEnabled = item is not null;
		if (_shown == (song, item))
		{
			return;
		}

		_shown = (song, item);
		var display = _controls.DisplayUpdater;
		display.ClearAll();
		if (song is not null)
		{
			display.Type = MediaPlaybackType.Music;
			display.MusicProperties.Title = song.Title;
			display.MusicProperties.Artist = song.Artist;
			display.MusicProperties.AlbumTitle = item?.Anime ?? song.Label;
			if (Uri.TryCreate(item?.Image, UriKind.Absolute, out var cover) && cover.Scheme is "https" or "http")
			{
				display.Thumbnail = RandomAccessStreamReference.CreateFromUri(cover);
			}
		}

		display.Update();
	}

	// Raised off the UI thread; the player and playlists are used on it only.
	private void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args) => _ui.TryEnqueue(() =>
	{
		switch (args.Button)
		{
			case SystemMediaTransportControlsButton.Play:
				_player.Resume();
				break;
			case SystemMediaTransportControlsButton.Pause:
				_player.Pause();
				break;
			case SystemMediaTransportControlsButton.Stop:
				_playlists.Stop();
				break;
			case SystemMediaTransportControlsButton.Next:
				_ = _playlists.NextAsync();
				break;
			case SystemMediaTransportControlsButton.Previous:
				_ = _playlists.PreviousAsync();
				break;
		}
	});

	// The thumbnail's buttons: always there once added (they can't be taken away), hidden while no song plays.
	private void UpdateButtons(bool playing, bool inPlaylist)
	{
		if (_taskbar is null)
		{
			return;
		}

		var hidden = playing ? 0 : ThumbButtonFlags.Hidden;
		var skip = hidden | (inPlaylist ? 0 : ThumbButtonFlags.Disabled);
		var paused = !_player.IsPlaying;
		ThumbButton[] buttons =
		[
			Button(PreviousButton, "Previous", "Previous", skip),
			Button(PlayPauseButton, paused ? "Play" : "Pause", paused ? "Play" : "Pause", hidden),
			Button(NextButton, "Next", "Next", skip),
		];
		if (_buttonsAdded)
		{
			_taskbar.ThumbBarUpdateButtons(_window, (uint)buttons.Length, buttons);
		}
		else
		{
			// Fails until the taskbar button's there; TaskbarButtonCreated tries again then.
			_buttonsAdded = _taskbar.ThumbBarAddButtons(_window, (uint)buttons.Length, buttons) >= 0;
		}
	}

	private ThumbButton Button(uint id, string icon, string tip, ThumbButtonFlags flags) => new()
	{
		Mask = ThumbButtonMask.Icon | ThumbButtonMask.Tooltip | ThumbButtonMask.Flags,
		Id = id,
		Icon = Icon(icon),
		Tip = tip,
		Flags = flags,
	};

	// White on a dark taskbar, black on a light one; loaded once each.
	private IntPtr Icon(string name)
	{
		var light = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;
		var file = $"{name}.{(light ? "Black" : "White")}.ico";
		if (!_icons.TryGetValue(file, out var icon))
		{
			var size = GetSystemMetrics(SmallIconWidth);
			_icons[file] = icon = LoadImage(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "Assets", "Taskbar", file), ImageIcon, size, size, LoadFromFile);
		}

		return icon;
	}

	private void OnWindowMessage(object? sender, WindowMessageEventArgs e)
	{
		const uint command = 0x0111, settingChange = 0x001A, clicked = 0x1800;
		if (e.Message.MessageId == _taskbarButtonCreated)
		{
			_buttonsAdded = false;
			Update();
		}
		else if (e.Message.MessageId == settingChange)
		{
			// The taskbar may have gone light or dark.
			Update();
		}
		else if (e.Message.MessageId == command && (e.Message.WParam >> 16 & 0xFFFF) == clicked)
		{
			switch ((uint)(e.Message.WParam & 0xFFFF))
			{
				case PreviousButton:
					_ = _playlists.PreviousAsync();
					break;
				case PlayPauseButton when _player.IsPlaying:
					_player.Pause();
					break;
				case PlayPauseButton:
					_player.Resume();
					break;
				case NextButton:
					_ = _playlists.NextAsync();
					break;
			}
		}
	}

	private const int SmallIconWidth = 49;
	private const uint ImageIcon = 1, LoadFromFile = 0x10;

	[LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
	private static partial uint RegisterWindowMessage(string name);

	[LibraryImport("user32.dll")]
	private static partial int GetSystemMetrics(int index);

	[LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint load);

	[Flags]
	private enum ThumbButtonMask : uint { Icon = 0x2, Tooltip = 0x4, Flags = 0x8 }

	[Flags]
	private enum ThumbButtonFlags : uint { Disabled = 0x1, Hidden = 0x8 }

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct ThumbButton
	{
		public ThumbButtonMask Mask;
		public uint Id;
		public uint Bitmap;
		public IntPtr Icon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Tip;
		public ThumbButtonFlags Flags;
	}

	[ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
	private class TaskbarList
	{
	}

	// ITaskbarList, 2 and 3's methods in their order, up to the thumbnail buttons'.
	[ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface ITaskbarList3
	{
		void HrInit();
		void AddTab(IntPtr hwnd);
		void DeleteTab(IntPtr hwnd);
		void ActivateTab(IntPtr hwnd);
		void SetActiveAlt(IntPtr hwnd);
		void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
		void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
		void SetProgressState(IntPtr hwnd, int flags);
		void RegisterTab(IntPtr tab, IntPtr mdi);
		void UnregisterTab(IntPtr tab);
		void SetTabOrder(IntPtr tab, IntPtr insertBefore);
		void SetTabActive(IntPtr tab, IntPtr mdi, uint reserved);
		[PreserveSig] int ThumbBarAddButtons(IntPtr hwnd, uint count, [MarshalAs(UnmanagedType.LPArray)] ThumbButton[] buttons);
		[PreserveSig] int ThumbBarUpdateButtons(IntPtr hwnd, uint count, [MarshalAs(UnmanagedType.LPArray)] ThumbButton[] buttons);
	}
}
