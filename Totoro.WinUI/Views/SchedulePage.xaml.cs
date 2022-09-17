using Totoro.Core.ViewModels;
using Microsoft.UI.Xaml;

namespace Totoro.WinUI.Views;


public class SchedulePageBase : ReactivePage<ScheduleViewModel> { }
public sealed partial class SchedulePage : SchedulePageBase
{
    public SchedulePage()
    {
        InitializeComponent();
    }

    public Visibility IsDayVisible(ScheduleModel model) => model.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
}
