using CommunityToolkit.WinUI.Notifications;
using MalApi;

namespace Totoro.WinUI.Services
{
    public class ToastService : IToastService
    {
        public void DownloadCompleted(string name)
        {
            new ToastContentBuilder().SetToastScenario(ToastScenario.Default)
                .AddText("Download Completed").AddText(name).Show();
        }
    }
}
