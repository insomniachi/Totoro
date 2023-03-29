namespace Totoro.WinUI.Media.Vlc;

/// <summary>
/// Interface to get display-related information for an application view
/// </summary>
internal interface IDisplayInformation
{
    /// <summary>
    /// Gets the scale factor
    /// </summary>
    double ScalingFactor { get; }
}
