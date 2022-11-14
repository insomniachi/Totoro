using System.IO;
using CommunityToolkit.WinUI.Notifications;

namespace Totoro.WinUI.Services
{
    public class ToastService : IToastService
    {
        public void DownloadCompleted(string directory, string fileName)
        {
            new ToastContentBuilder()
                .SetToastScenario(ToastScenario.Default)
                .AddText("Download Completed")
                .AddText(fileName)
                .AddArgument("Type", ToastType.DownloadComplete)
                .AddArgument("File", Path.Combine(directory, fileName))
                .AddArgument("NeedUI", true)
                .Show();
        }
    }

    public enum ToastType
    {
        DownloadComplete
    }
}
