using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.IO;
using System.Text.Json;

namespace Totoro.WinUI.Services
{
    public class ToastService : IToastService
    {
        public void CheckEpisodeComplete(AnimeModel anime, int currentEp)
        {
            var payload = JsonSerializer.Serialize(anime);
            new AppNotificationBuilder()
                .SetScenario(AppNotificationScenario.Reminder)
                .AddText($"Did you finish watching {anime.Title} Episode {currentEp}")
                .AddButton(new AppNotificationButton("Yes")
                    .AddArgument("Type", ToastType.FinishedEpisode.ToString())
                    .AddArgument("Payload", payload)
                    .AddArgument("Episode", currentEp.ToString()))
                .AddButton(new AppNotificationButton("No")
                    .AddArgument("Type", ToastType.NoAction.ToString()))
                .BuildNotification()
                .Show();
        }

        public void Playing(AnimeModel anime, string episode)
        {
            new AppNotificationBuilder()
                .SetScenario(AppNotificationScenario.Default)
                .SetDuration(AppNotificationDuration.Default)
                .AddText(anime.Title)
                .AddText($"Episode {episode}")
                .BuildNotification()
                .Show();
        }

        public void DownloadCompleted(string directory, string fileName)
        {
            new AppNotificationBuilder()
                .SetScenario(AppNotificationScenario.Default)
                .AddText("Download Completed")
                .AddText(fileName)
                .AddArgument("Type", ToastType.DownloadComplete.ToString())
                .AddArgument("File", Path.Combine(directory, fileName))
                .AddArgument("NeedUI", bool.TrueString)
                .BuildNotification()
                .Show();
        }

        public void PromptAnimeSelection(IEnumerable<AnimeModel> items, AnimeModel defaultSelection)
        {
            var cb = new AppNotificationComboBox("animeId");

            foreach (var item in items)
            {
                cb.AddItem(item.Id.ToString(), item.Title);
            }

            cb.SelectedItem = defaultSelection.Id.ToString();

            new AppNotificationBuilder()
                .SetScenario(AppNotificationScenario.Reminder)
                .AddText($"Unable match anime, select from the list")
                .AddComboBox(cb)
                .AddButton(new AppNotificationButton("Select").AddArgument("Type", ToastType.SelectAnime.ToString()))
                .BuildNotification()
                .Show();
        }
    }

    public static class NotificationExtensions
    {
        public static void Show(this AppNotification notification)
        {
            AppNotificationManager.Default.Show(notification);
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
