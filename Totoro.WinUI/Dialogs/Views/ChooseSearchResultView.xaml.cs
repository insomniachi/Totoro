using AnimDL.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

public class ChooseSearchResultViewBase : ReactivePage<ChooseSearchResultViewModel> { }
public sealed partial class ChooseSearchResultView : ChooseSearchResultViewBase
{
    public ChooseSearchResultView()
    {
        InitializeComponent();
    }
}
