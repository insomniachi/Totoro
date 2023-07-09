namespace Totoro.Plugins.MediaDetection.Generic;

public class MpcConfig
{
    public static string FileName { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"MPC-HC\mpc-hc64.exe");
}

public class MpvConfig
{
    public static string FileName { get; set; } = @"";
}    
