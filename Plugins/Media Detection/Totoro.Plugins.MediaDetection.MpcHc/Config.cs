using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.MpcHc;

public class Config : ConfigObject
{
    public string FileName { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"MPC-HC\mpc-hc64.exe");
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 13579;
}