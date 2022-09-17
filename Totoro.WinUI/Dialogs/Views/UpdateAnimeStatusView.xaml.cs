using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

public class UpdateAnimeStatusViewBase : ReactivePage<UpdateAnimeStatusViewModel> { }
public sealed partial class UpdateAnimeStatusView : UpdateAnimeStatusViewBase
{
    public List<AnimeStatus> Statuses { get; } = Enum.GetValues<AnimeStatus>().Cast<AnimeStatus>().Take(5).ToList();
    public UpdateAnimeStatusView()
    {
        InitializeComponent();
    }
}
