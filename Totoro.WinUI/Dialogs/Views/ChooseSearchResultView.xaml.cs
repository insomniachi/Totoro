using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Dialogs.Views;

public class ChooseSearchResultViewBase : ReactiveContentDialog<ChooseSearchResultViewModel> { }
public sealed partial class ChooseSearchResultView : ChooseSearchResultViewBase
{
    public ChooseSearchResultView()
    {
        InitializeComponent();
    }
}
