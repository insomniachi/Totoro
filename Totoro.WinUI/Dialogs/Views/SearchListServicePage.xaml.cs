using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Dialogs.Views;

public class SearchListServicePageBase : ReactiveContentDialog<SearchListServiceViewModel> { }

public sealed partial class SearchListServicePage : SearchListServicePageBase
{
    public SearchListServicePage()
    {
        InitializeComponent();
    }
}
