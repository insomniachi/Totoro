using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Totoro.Modules.MediaPlayers.Mpv;

/// <summary>
/// The few calls of libmpv's C API (client.h) the built-in mpv player makes, from the libmpv-2.dll downloaded with mpv
/// (see <see cref="MpvInstaller"/>). Loaded by path, as it isn't beside the app: <see cref="Load"/> first. Calls are
/// thread-safe, but <see cref="WaitEvent"/> is for one thread at a time.
/// </summary>
internal static unsafe partial class LibMpvNative
{
	private const string Library = "libmpv-2";

	private static readonly Lock s_lock = new();
	private static nint s_library;

	public static string FilePath => Path.Combine(MpvInstaller.Folder, "libmpv-2.dll");

	/// <summary>Whether it's loaded, so it can't be replaced until the app restarts.</summary>
	public static bool IsLoaded => s_library != 0;

	/// <summary>Loads libmpv, once, for the calls here to find; throws if it isn't there or isn't one.</summary>
	public static void Load()
	{
		lock (s_lock)
		{
			if (s_library != 0)
			{
				return;
			}

			var library = NativeLibrary.Load(FilePath);
			// Before s_library is set, so the resolver's in place whenever IsLoaded says it is.
			NativeLibrary.SetDllImportResolver(typeof(LibMpvNative).Assembly, Resolve);
			s_library = library;
		}
	}

	// The app's other imports (user32...) are found as usual.
	private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath) => name == Library ? s_library : 0;

	/// <summary>A new mpv, to set options on and then <see cref="Initialize"/>; throws if it can't be made.</summary>
	public static MpvHandle Create()
	{
		var mpv = CreateNative();
		return mpv.IsInvalid ? throw new InvalidOperationException("mpv couldn't be created.") : mpv;
	}

	[LibraryImport(Library, EntryPoint = "mpv_create")]
	private static partial MpvHandle CreateNative();

	[LibraryImport(Library, EntryPoint = "mpv_initialize")]
	public static partial int Initialize(MpvHandle mpv);

	/// <summary>Ends playback and frees mpv: <see cref="MpvHandle"/>'s, once nothing's using it.</summary>
	[LibraryImport(Library, EntryPoint = "mpv_terminate_destroy")]
	internal static partial void TerminateDestroy(nint mpv);

	[LibraryImport(Library, EntryPoint = "mpv_set_option_string", StringMarshalling = StringMarshalling.Utf8)]
	public static partial int SetOptionString(MpvHandle mpv, string name, string value);

	[LibraryImport(Library, EntryPoint = "mpv_set_property_string", StringMarshalling = StringMarshalling.Utf8)]
	public static partial int SetPropertyString(MpvHandle mpv, string name, string value);

	/// <summary>A property as text; null if it's unavailable (e.g. no video loaded).</summary>
	[LibraryImport(Library, EntryPoint = "mpv_get_property_string", StringMarshalling = StringMarshalling.Utf8)]
	[return: MarshalUsing(typeof(MpvStringMarshaller))]
	public static partial string? GetPropertyString(MpvHandle mpv, string name);

	/// <summary>Runs a command, e.g. "loadfile", url, ...; its arguments go as they are (no quoting).</summary>
	public static int Command(MpvHandle mpv, params ReadOnlySpan<string> args)
	{
		// Each argument null-terminated, one after another in one buffer, with a null-terminated list of where each starts.
		var size = 0;
		foreach (var arg in args)
		{
			size += Encoding.UTF8.GetByteCount(arg) + 1;
		}

		Span<byte> buffer = size <= 1024 ? stackalloc byte[size] : new byte[size];
		var pointers = stackalloc byte*[args.Length + 1];
		fixed (byte* start = buffer)
		{
			var at = 0;
			for (var i = 0; i < args.Length; i++)
			{
				pointers[i] = start + at;
				at += Encoding.UTF8.GetBytes(args[i], buffer[at..]);
				buffer[at++] = 0;
			}

			pointers[args.Length] = null;
			return CommandNative(mpv, pointers);
		}
	}

	[LibraryImport(Library, EntryPoint = "mpv_command")]
	private static partial int CommandNative(MpvHandle mpv, byte** args);

	/// <summary>Has a property's changes sent as <see cref="MpvEventId.PropertyChange"/> events, in <paramref name="format"/>.</summary>
	public static int ObserveProperty(MpvHandle mpv, string name, MpvFormat format) => ObservePropertyNative(mpv, 0, name, format);

	[LibraryImport(Library, EntryPoint = "mpv_observe_property", StringMarshalling = StringMarshalling.Utf8)]
	private static partial int ObservePropertyNative(MpvHandle mpv, ulong replyUserdata, string name, MpvFormat format);

	/// <summary>The next event, waiting for one; valid until the next call. mpv isn't freed while one waits.</summary>
	public static Event* WaitEvent(MpvHandle mpv) => WaitEventNative(mpv, -1);

	[LibraryImport(Library, EntryPoint = "mpv_wait_event")]
	private static partial Event* WaitEventNative(MpvHandle mpv, double timeout);

	[LibraryImport(Library, EntryPoint = "mpv_free")]
	private static partial void Free(void* data);

	/// <summary>A string mpv made, read and then freed with mpv_free.</summary>
	[CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedOut, typeof(MpvStringMarshaller))]
	internal static class MpvStringMarshaller
	{
		public static string? ConvertToManaged(byte* value) => value is null ? null : Marshal.PtrToStringUTF8((nint)value);

		public static void Free(byte* value)
		{
			if (value is not null)
			{
				LibMpvNative.Free(value);
			}
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Event
	{
		public MpvEventId Id;
		public int Error;
		public ulong ReplyUserdata;
		public void* Data;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct PropertyData
	{
		public byte* Name;
		public MpvFormat Format;
		public void* Data;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct EndFileData
	{
		public MpvEndFileReason Reason;
		public int Error;
	}
}

/// <summary>
/// An mpv (mpv_handle), freed with mpv_terminate_destroy once disposed and no call is using it: a call on one disposed
/// throws rather than reaching mpv, and one waiting for an event keeps it until it returns.
/// </summary>
internal sealed class MpvHandle() : SafeHandleZeroOrMinusOneIsInvalid(ownsHandle: true)
{
	protected override bool ReleaseHandle()
	{
		LibMpvNative.TerminateDestroy(handle);
		return true;
	}
}

/// <summary>The events (mpv_event_id) the player handles.</summary>
internal enum MpvEventId
{
	Shutdown = 1,
	StartFile = 6,
	EndFile = 7,
	FileLoaded = 8,
	VideoReconfig = 17,
	PlaybackRestart = 21,
	PropertyChange = 22,
}

/// <summary>The value formats (mpv_format) the player uses.</summary>
internal enum MpvFormat
{
	None = 0,
	Flag = 3,
	Double = 5,
}

/// <summary>Why a video ended (mpv_end_file_reason).</summary>
internal enum MpvEndFileReason
{
	Eof = 0,
	Stop = 2,
	Quit = 3,
	Error = 4,
	Redirect = 5,
}
