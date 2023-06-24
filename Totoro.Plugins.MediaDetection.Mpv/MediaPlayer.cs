namespace Totoro.Plugins.MediaDetection.Generic;

public class Mpv : GenericMediaPlayer
{
    protected override string ParseFromWindowTitle(string windowTitle)
    {
        return windowTitle.Replace("- mpv", string.Empty);
    }
}

public class MpcHc : GenericMediaPlayer 
{
    public MpcHc()
    {
        GetTitleFromWindow = true;
    }
}
