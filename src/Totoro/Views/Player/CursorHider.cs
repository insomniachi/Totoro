using System.Runtime.InteropServices;

namespace Totoro.Views.Player;

/// <summary>
/// Hides the pointer over the video while the player's controls are hidden. WinUI sets the cursor itself (from a thread
/// of its own), so neither ShowCursor nor an element's blank cursor keeps it hidden while the pointer is still: this
/// puts a plain, invisible Win32 window over the video instead, which shows no cursor, and says when the mouse is used
/// over it (moved, clicked), for the controls to come back. It never takes focus, so keys still reach the player.
/// It stays over the video for as long as the window's open, and only stops letting the mouse through: a window
/// appearing over the video makes Windows compose it differently, which shows as a hitch.
/// </summary>
internal sealed unsafe partial class CursorHider : IDisposable
{
	private const string ClassName = "TotoroCursorHider";
	private static readonly Dictionary<IntPtr, CursorHider> s_hiders = [];
	private static bool s_isRegistered;

	private readonly IntPtr _window;
	// Where the pointer was when it started hiding it: the system sends a move then, which isn't one.
	private Point _shownAt;

	public CursorHider(IntPtr parent)
	{
		var instance = GetModuleHandleW(null);
		if (!s_isRegistered)
		{
			fixed (char* name = ClassName)
			{
				var windowClass = new WindowClass
				{
					Size = (uint)sizeof(WindowClass),
					WindowProcedure = &WindowProcedure,
					Instance = instance,
					Background = GetStockObject(4), // BLACK_BRUSH, at 1/255 opacity (below)
					ClassName = name,
				};
				RegisterClassExW(&windowClass);
			}

			s_isRegistered = true;
		}

		// WS_CHILD | WS_CLIPSIBLINGS | WS_VISIBLE; WS_EX_NOACTIVATE | WS_EX_LAYERED at 1/255 opacity: unseen, yet it can get
		// the mouse (a fully see-through window couldn't). WS_EX_TRANSPARENT lets the mouse through until the cursor's hidden
		// (WinUI takes its input on a thread of its own, so a WM_NCHITTEST of HTTRANSPARENT can't pass it on).
		_window = CreateWindowExW(NoActivate | Layered | Transparent, ClassName, null, 0x40000000 | 0x04000000 | 0x10000000, 0, 0, 0, 0, parent, IntPtr.Zero, instance, IntPtr.Zero);
		SetLayeredWindowAttributes(_window, 0, 1, 0x2); // LWA_ALPHA
		s_hiders[_window] = this;
	}

	private const int NoActivate = 0x08000000;
	private const int Layered = 0x00080000;
	private const int Transparent = 0x00000020;

	/// <summary>Raised when the mouse is moved or clicked over it while it hides the cursor.</summary>
	public event EventHandler? Used;

	/// <summary>Covers the parent's client area from <paramref name="x"/>, <paramref name="y"/> (pixels) down to its bottom right.</summary>
	public void Place(int x, int y)
	{
		// HWND_TOP, SWP_NOACTIVATE: above WinUI's content (and mpv's window), never focused. As big as every screen together,
		// clipped to the parent, so it's never resized: a resized layered window shows opaque for a frame (going full screen).
		SetWindowPos(_window, IntPtr.Zero, x, y, GetSystemMetrics(78), GetSystemMetrics(79), 0x0010); // SM_CXVIRTUALSCREEN, SM_CYVIRTUALSCREEN
	}

	/// <summary>Whether it hides the cursor (and catches the mouse), rather than letting the mouse through.</summary>
	public bool IsHidingCursor
	{
		set
		{
			SetWindowLongW(_window, -20, NoActivate | Layered | (value ? 0 : Transparent)); // GWL_EXSTYLE
			if (value)
			{
				GetCursorPos(out _shownAt);
				// SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED: has the system look again at what's
				// under the still pointer, which sets the cursor now rather than at the next move. Not when the cursor comes
				// back: the mouse is moving then anyway, and the refresh shows as a hitch in the video.
				SetWindowPos(_window, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
			}
		}
	}

	public void Dispose()
	{
		s_hiders.Remove(_window);
		DestroyWindow(_window);
	}

	[UnmanagedCallersOnly]
	private static IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
	{
		switch (message)
		{
			case 0x0020: // WM_SETCURSOR
				SetCursor(IntPtr.Zero);
				return 1;
			case 0x0021: // WM_MOUSEACTIVATE
				return 3; // MA_NOACTIVATE
			case 0x0200 when s_hiders.TryGetValue(window, out var hider): // WM_MOUSEMOVE
				GetCursorPos(out var point);
				if (point.X != hider._shownAt.X || point.Y != hider._shownAt.Y)
				{
					hider.Used?.Invoke(hider, EventArgs.Empty);
				}

				return 0;
			case 0x0201 or 0x0204 or 0x0207 or 0x020B when s_hiders.TryGetValue(window, out var hider): // a button down
				hider.Used?.Invoke(hider, EventArgs.Empty);
				return 0;
		}

		return DefWindowProcW(window, message, wParam, lParam);
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Point
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct WindowClass
	{
		public uint Size;
		public uint Style;
		public delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, IntPtr> WindowProcedure;
		public int ClassExtra;
		public int WindowExtra;
		public IntPtr Instance;
		public IntPtr Icon;
		public IntPtr Cursor;
		public IntPtr Background;
		public char* MenuName;
		public char* ClassName;
		public IntPtr SmallIcon;
	}

	[LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr GetModuleHandleW(string? name);

	[LibraryImport("user32.dll")]
	private static partial ushort RegisterClassExW(WindowClass* windowClass);

	[LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr CreateWindowExW(uint exStyle, string className, string? name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

	[LibraryImport("user32.dll")]
	private static partial IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);


	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool DestroyWindow(IntPtr window);

	[LibraryImport("user32.dll")]
	private static partial int SetWindowLongW(IntPtr window, int index, int value);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool SetLayeredWindowAttributes(IntPtr window, uint key, byte alpha, uint flags);

	[LibraryImport("user32.dll")]
	private static partial int GetSystemMetrics(int index);

	[LibraryImport("gdi32.dll")]
	private static partial IntPtr GetStockObject(int index);

	[LibraryImport("user32.dll")]
	private static partial IntPtr SetCursor(IntPtr cursor);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetCursorPos(out Point point);
}
