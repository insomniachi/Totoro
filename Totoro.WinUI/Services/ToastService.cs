using System.IO;
using System.Text.Json;
using CommunityToolkit.WinUI.Notifications;

namespace Totoro.WinUI.Services
{
    public class ToastService : IToastService
    {
        public void CheckEpisodeComplete(AnimeModel anime, int currentEp)
        {
            var payload = JsonSerializer.Serialize(anime);
            new ToastContentBuilder()
                .SetToastScenario(ToastScenario.Reminder)
                .AddText($"Did you finish watching {anime.Title} Episode {currentEp}")
                .AddButton("Yes", ToastActivationType.Background, $"Type={ToastType.FinishedEpisode};Payload={payload}")
                .AddButton("No", ToastActivationType.Background, $"Type={ToastType.NoAction}")
                .Show();
        }

        public void Playing(AnimeModel anime, string episode)
        {
            new ToastContentBuilder()
                .SetToastScenario(ToastScenario.Default)
                .SetToastDuration(ToastDuration.Short)
                .AddText("Now Playing", AdaptiveTextStyle.Header)
                .AddText($"{anime.Title} Episode {episode}", AdaptiveTextStyle.Subheader)
                .Show();
        }

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
        DownloadComplete,
        FinishedEpisode,
        NoAction
    }
}
