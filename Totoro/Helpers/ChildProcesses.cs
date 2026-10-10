using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Totoro.Helpers;

/// <summary>
/// Ties background processes (e.g. a torrent streaming server) to the app's lifetime: Windows ends them when the app
/// exits, however it exits, crashes included. Uses a job object that kills its processes when its last handle closes,
/// which happens when the app's process ends.
/// </summary>
internal static class ChildProcesses
{
	private const int JobObjectExtendedLimitInformation = 9;
	private const uint JobObjectLimitKillOnJobClose = 0x2000;

	private static readonly Lazy<IntPtr> s_job = new(CreateJob);

	/// <summary>Ends <paramref name="process"/> when the app exits. Returns false if that couldn't be arranged.</summary>
	public static bool Add(Process process)
	{
		return s_job.Value != IntPtr.Zero && AssignProcessToJobObject(s_job.Value, process.Handle);
	}

	private static IntPtr CreateJob()
	{
		var job = CreateJobObject(IntPtr.Zero, null);
		if (job == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}

		var info = new JobObjectExtendedLimitInfo { BasicLimitInformation = { LimitFlags = JobObjectLimitKillOnJobClose } };
		var size = Marshal.SizeOf<JobObjectExtendedLimitInfo>();
		var pointer = Marshal.AllocHGlobal(size);
		try
		{
			Marshal.StructureToPtr(info, pointer, false);
			return SetInformationJobObject(job, JobObjectExtendedLimitInformation, pointer, (uint)size) ? job : IntPtr.Zero;
		}
		finally
		{
			Marshal.FreeHGlobal(pointer);
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

	[StructLayout(LayoutKind.Sequential)]
	private struct JobObjectBasicLimitInfo
	{
		public long PerProcessUserTimeLimit;
		public long PerJobUserTimeLimit;
		public uint LimitFlags;
		public UIntPtr MinimumWorkingSetSize;
		public UIntPtr MaximumWorkingSetSize;
		public uint ActiveProcessLimit;
		public UIntPtr Affinity;
		public uint PriorityClass;
		public uint SchedulingClass;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct IoCounters
	{
		public ulong ReadOperationCount;
		public ulong WriteOperationCount;
		public ulong OtherOperationCount;
		public ulong ReadTransferCount;
		public ulong WriteTransferCount;
		public ulong OtherTransferCount;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct JobObjectExtendedLimitInfo
	{
		public JobObjectBasicLimitInfo BasicLimitInformation;
		public IoCounters IoInfo;
		public UIntPtr ProcessMemoryLimit;
		public UIntPtr JobMemoryLimit;
		public UIntPtr PeakProcessMemoryUsed;
		public UIntPtr PeakJobMemoryUsed;
	}
}
