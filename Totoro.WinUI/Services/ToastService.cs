using System.IO;
using System.Text.Json;
using CommunityToolkit.WinUI.Notifications;
using Windows.ApplicationModel.Store;

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
                .AddText(anime.Title, AdaptiveTextStyle.Subheader)
                .AddText($"Episode {episode}")
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

        public void PromptAnimeSelection(IEnumerable<AnimeModel> items, AnimeModel defaultSelection)
        {
            new ToastContentBuilder()
                .SetToastScenario(ToastScenario.Reminder)
                .AddText($"Unable match anime, select from the list", AdaptiveTextStyle.Header)
                .AddComboBox("animeId", defaultSelection.Id.ToString(), items.Select(x => new ValueTuple<string,string>(x.Id.ToString(), x.Title)).ToArray())
                .AddButton("Select", ToastActivationType.Background, $"Type={ToastType.SelectAnime}")
                .Show();
        }
    }

    public enum ToastType
    {
        DownloadComplete,
        FinishedEpisode,
        NoAction,
        SelectAnime
    }
}
