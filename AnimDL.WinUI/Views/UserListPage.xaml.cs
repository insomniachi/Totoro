using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using AnimDL.UI.Core.Models;
using AnimDL.WinUI.ViewModels;
using ReactiveUI;

namespace AnimDL.WinUI.Views;

public class UserListPageBase : ReactivePage<UserListViewModel> { }
public sealed partial class UserListPage : UserListPageBase
{
    public List<AnimeStatus> Statuses { get; set; } = new List<AnimeStatus>
    {
        AnimeStatus.Watching,
        AnimeStatus.PlanToWatch,
        AnimeStatus.OnHold,
        AnimeStatus.Completed,
        AnimeStatus.Dropped
    };

    public static string ToStatusString(AnimeStatus status)
    {
        return status switch
        {
            AnimeStatus.Watching => "Watching",
            AnimeStatus.PlanToWatch => "Plan to watch",
            AnimeStatus.OnHold => "On Hold",
            AnimeStatus.Completed => "Completed",
            AnimeStatus.Dropped => "Dropped",
            _ => throw new ArgumentException(null, nameof(status))
        };
    }

    public UserListPage()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.ViewModel.CurrentView)
            .Subscribe(x =>
            {
                switch (x)
                {
                    case AnimeStatus.Watching:
                        WatchingFlyoutToggle.IsChecked = true;
                        break;
                    case AnimeStatus.PlanToWatch:
                        PtwFlyoutToggle.IsChecked = true;
                        break;
                    case AnimeStatus.Completed:
                        CompletedFlyoutToggle.IsChecked = true;
                        break;
                    case AnimeStatus.OnHold:
                        OnHoldFlyoutToggle.IsChecked = true;
                        break;
                    case AnimeStatus.Dropped:
                        DroppedFlyoutToggle.IsChecked = true;
                        break;
                }
            });
    }
}

public enum DisplayMode
{
    Grid,
    List
}
