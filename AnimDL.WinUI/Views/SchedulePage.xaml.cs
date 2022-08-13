using AnimDL.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using ReactiveUI;

namespace AnimDL.WinUI.Views;


public class SchedulePageBase : ReactivePage<ScheduleViewModel> { }
public sealed partial class SchedulePage : SchedulePageBase
{
    public SchedulePage()
    {
        InitializeComponent();
    }

    public Visibility IsDayVisible(ScheduleModel model) => model.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
}
