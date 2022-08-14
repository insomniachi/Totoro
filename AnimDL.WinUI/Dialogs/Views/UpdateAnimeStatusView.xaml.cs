using System;
using System.Collections.Generic;
using System.Linq;
using AnimDL.WinUI.Dialogs.ViewModels;
using MalApi;
using ReactiveUI;

namespace AnimDL.WinUI.Dialogs.Views;

public class UpdateAnimeStatusViewBase : ReactivePage<UpdateAnimeStatusViewModel> { }
public sealed partial class UpdateAnimeStatusView : UpdateAnimeStatusViewBase
{
    public List<AnimeStatus> Statuses { get; } = Enum.GetValues<AnimeStatus>().Cast<AnimeStatus>().Take(5).ToList();
    public List<Score> Scores { get; } = Enum.GetValues<Score>().Cast<Score>().Reverse().ToList();

    public UpdateAnimeStatusView()
    {
        InitializeComponent();
    }
}
