namespace Totoro.Core.Models;

public enum PlaybackRate
{
    One,
    OnePointTwoFive,
    OnePointFive,
    Two
}

public static class PlaybackRateExtenstions
{
    public static double ToDouble(this PlaybackRate rate)
    {
        return rate switch
        {
            PlaybackRate.OnePointTwoFive => 1.25,
            PlaybackRate.OnePointFive => 1.5,
            PlaybackRate.Two => 2,
            _ => 1
        };

    }

    public static float ToFloat(this PlaybackRate rate)
    {
        return rate switch
        {
            PlaybackRate.OnePointTwoFive => 1.25f,
            PlaybackRate.OnePointFive => 1.5f,
            PlaybackRate.Two => 2,
            _ => 1
        };
    }
}
