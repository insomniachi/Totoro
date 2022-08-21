using AnimDL.UI.Core.ViewModels;
using Microsoft.UI.Xaml;

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
