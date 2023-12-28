using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Generic;

public class MpcConfig : ConfigObject
{
    public string FileName { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"MPC-HC\mpc-hc64.exe");
}

public class MpvConfig : ConfigObject
{
    public string FileName { get; set; } = @"";
}
