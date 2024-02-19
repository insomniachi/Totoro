using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Dialogs.Views;

public class EditAnimePreferencesViewBase : ReactiveContentDialog<EditAnimePreferencesViewModel> { }

public sealed partial class EditAnimePreferencesView : EditAnimePreferencesViewBase
{
    public EditAnimePreferencesView()
    {
        InitializeComponent();
    }
}
