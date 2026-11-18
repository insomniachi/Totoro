using System.Runtime.InteropServices;
using System.Text;

namespace Totoro.Modules.MediaPlayers.Mpv;

/// <summary>
/// The few calls of libmpv's C API (client.h) the built-in mpv player makes, from the libmpv-2.dll downloaded with mpv
/// (see <see cref="MpvInstaller"/>). Loaded by path, as it isn't beside the app. Calls are thread-safe, but
/// <see cref="WaitEvent"/> is for one thread at a time.
/// </summary>
internal static unsafe class LibMpvNative
{
	public const int FormatNone = 0;
	public const int FormatFlag = 3;
	public const int FormatDouble = 5;

	public const int EventShutdown = 1;
	public const int EventStartFile = 6;
	public const int EventEndFile = 7;
	public const int EventFileLoaded = 8;
	public const int EventVideoReconfig = 17;
	public const int EventPlaybackRestart = 21;
	public const int EventPropertyChange = 22;

	public const int EndFileReasonError = 4;

	private static readonly Lock s_lock = new();
	private static nint s_library;

	private static delegate* unmanaged<nint> s_create;
	private static delegate* unmanaged<nint, int> s_initialize;
	private static delegate* unmanaged<nint, void> s_terminateDestroy;
	private static delegate* unmanaged<nint, byte*, byte*, int> s_setOptionString;
	private static delegate* unmanaged<nint, byte*, byte*, int> s_setPropertyString;
	private static delegate* unmanaged<nint, byte*, byte*> s_getPropertyString;
	private static delegate* unmanaged<nint, byte**, int> s_command;
	private static delegate* unmanaged<nint, ulong, byte*, int, int> s_observeProperty;
	private static delegate* unmanaged<nint, double, Event*> s_waitEvent;
	private static delegate* unmanaged<void*, void> s_free;

	public static string FilePath => Path.Combine(MpvInstaller.Folder, "libmpv-2.dll");

	/// <summary>Whether it's loaded, so it can't be replaced until the app restarts.</summary>
	public static bool IsLoaded => s_library != 0;

	/// <summary>Loads libmpv, once; throws if it isn't there or isn't one.</summary>
	public static void Load()
	{
		lock (s_lock)
		{
			if (s_library != 0)
			{
				return;
			}

			var library = NativeLibrary.Load(FilePath);
			s_create = (delegate* unmanaged<nint>)NativeLibrary.GetExport(library, "mpv_create");
			s_initialize = (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(library, "mpv_initialize");
			s_terminateDestroy = (delegate* unmanaged<nint, void>)NativeLibrary.GetExport(library, "mpv_terminate_destroy");
			s_setOptionString = (delegate* unmanaged<nint, byte*, byte*, int>)NativeLibrary.GetExport(library, "mpv_set_option_string");
			s_setPropertyString = (delegate* unmanaged<nint, byte*, byte*, int>)NativeLibrary.GetExport(library, "mpv_set_property_string");
			s_getPropertyString = (delegate* unmanaged<nint, byte*, byte*>)NativeLibrary.GetExport(library, "mpv_get_property_string");
			s_command = (delegate* unmanaged<nint, byte**, int>)NativeLibrary.GetExport(library, "mpv_command");
			s_observeProperty = (delegate* unmanaged<nint, ulong, byte*, int, int>)NativeLibrary.GetExport(library, "mpv_observe_property");
			s_waitEvent = (delegate* unmanaged<nint, double, Event*>)NativeLibrary.GetExport(library, "mpv_wait_event");
			s_free = (delegate* unmanaged<void*, void>)NativeLibrary.GetExport(library, "mpv_free");
			s_library = library;
		}
	}

	public static nint Create() => s_create();

	public static int Initialize(nint mpv) => s_initialize(mpv);

	/// <summary>Ends playback and frees mpv; not while another thread is in <see cref="WaitEvent"/>.</summary>
	public static void TerminateDestroy(nint mpv) => s_terminateDestroy(mpv);

	public static int SetOptionString(nint mpv, string name, string value)
	{
		fixed (byte* n = Utf8(name), v = Utf8(value))
		{
			return s_setOptionString(mpv, n, v);
		}
	}

	public static int SetPropertyString(nint mpv, string name, string value)
	{
		fixed (byte* n = Utf8(name), v = Utf8(value))
		{
			return s_setPropertyString(mpv, n, v);
		}
	}

	/// <summary>A property as text; null if it's unavailable (e.g. no video loaded).</summary>
	public static string? GetPropertyString(nint mpv, string name)
	{
		fixed (byte* n = Utf8(name))
		{
			var value = s_getPropertyString(mpv, n);
			if (value is null)
			{
				return null;
			}

			try
			{
				return Marshal.PtrToStringUTF8((nint)value);
			}
			finally
			{
				s_free(value);
			}
		}
	}

	public static int Command(nint mpv, params string[] args)
	{
		var handles = new GCHandle[args.Length];
		var pointers = new byte*[args.Length + 1];
		try
		{
			for (var i = 0; i < args.Length; i++)
			{
				handles[i] = GCHandle.Alloc(Utf8(args[i]), GCHandleType.Pinned);
				pointers[i] = (byte*)handles[i].AddrOfPinnedObject();
			}

			fixed (byte** p = pointers)
			{
				return s_command(mpv, p);
			}
		}
		finally
		{
			foreach (var handle in handles)
			{
				if (handle.IsAllocated)
				{
					handle.Free();
				}
			}
		}
	}

	public static int ObserveProperty(nint mpv, string name, int format)
	{
		fixed (byte* n = Utf8(name))
		{
			return s_observeProperty(mpv, 0, n, format);
		}
	}

	/// <summary>The next event, waiting for one; valid until the next call.</summary>
	public static Event* WaitEvent(nint mpv) => s_waitEvent(mpv, -1);

	// Null-terminated, as C wants.
	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + '\0');

	[StructLayout(LayoutKind.Sequential)]
	public struct Event
	{
		public int Id;
		public int Error;
		public ulong ReplyUserdata;
		public void* Data;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct PropertyData
	{
		public byte* Name;
		public int Format;
		public void* Data;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct EndFileData
	{
		public int Reason;
		public int Error;
	}
}
