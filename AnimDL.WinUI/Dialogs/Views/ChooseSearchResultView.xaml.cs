using AnimDL.WinUI.Dialogs.ViewModels;
using Microsoft.UI.Xaml.Controls;
using ReactiveUI;


namespace AnimDL.WinUI.Dialogs.Views;

public class ChooseSearchResultViewBase : ReactivePage<ChooseSearchResultViewModel> { }
public sealed partial class ChooseSearchResultView : ChooseSearchResultViewBase
{
    public ChooseSearchResultView()
    {
        this.InitializeComponent();
    }
}
