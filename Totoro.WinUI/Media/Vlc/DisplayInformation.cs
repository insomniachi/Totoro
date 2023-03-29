
namespace Totoro.WinUI.Media.Vlc;


/// <summary>
/// Monitors display-related information for an application view
/// </summary>
internal class DisplayInformation : IDisplayInformation
{
    /// <summary>
    /// Gets the scale factor
    /// </summary>
    public double ScalingFactor => App.MainWindow.Content.XamlRoot.RasterizationScale;

}