namespace Totoro.Core.Models;

public enum PlaybackRate
{
    One,
    OnePointTwoFive,
    OnePointFive,
    OnePointSevenFive,
    Two,
    TwoPointFive,
    Four,
}

public static class PlaybackRateExtenstions
{
    public static double ToDouble(this PlaybackRate rate)
    {
        return rate switch
        {
            PlaybackRate.OnePointTwoFive => 1.25,
            PlaybackRate.OnePointFive => 1.5,
            PlaybackRate.OnePointSevenFive => 1.75,
            PlaybackRate.Two => 2,
            PlaybackRate.TwoPointFive => 2.5,
            PlaybackRate.Four => 4,
            _ => 1
        };

    }

    public static float ToFloat(this PlaybackRate rate)
    {
        return rate switch
        {
            PlaybackRate.OnePointTwoFive => 1.25f,
            PlaybackRate.OnePointFive => 1.5f,
            PlaybackRate.OnePointSevenFive => 1.75f,
            PlaybackRate.Two => 2,
            PlaybackRate.TwoPointFive => 2.5f,
            PlaybackRate.Four => 4f,
            _ => 1
        };
    }
}
