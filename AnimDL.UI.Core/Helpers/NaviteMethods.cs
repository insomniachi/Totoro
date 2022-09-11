using System.Runtime.InteropServices;

namespace AnimDL.UI.Core.Helpers;

internal static class NativeMethods
{

    public static void PreventSleep()
    {
        SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsDisplayRequired);
    }

    public static void AllowSleep()
    {
        SetThreadExecutionState(ExecutionState.EsContinuous);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [Flags]
    private enum ExecutionState : uint
    {
        EsAwaymodeRequired = 0x00000040,
        EsContinuous = 0x80000000,
        EsDisplayRequired = 0x00000002,
        EsSystemRequired = 0x00000001
    }
}
