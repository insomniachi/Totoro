using System;
using System.Collections.Generic;
using System.Linq;
using AnimDL.UI.Core.Models;
using AnimDL.WinUI.Dialogs.ViewModels;
using ReactiveUI;

namespace AnimDL.WinUI.Dialogs.Views;

public class UpdateAnimeStatusViewBase : ReactivePage<UpdateAnimeStatusViewModel> { }
public sealed partial class UpdateAnimeStatusView : UpdateAnimeStatusViewBase
{
    public List<AnimeStatus> Statuses { get; } = Enum.GetValues<AnimeStatus>().Cast<AnimeStatus>().Take(5).ToList();
    public UpdateAnimeStatusView()
    {
        InitializeComponent();
    }
}
