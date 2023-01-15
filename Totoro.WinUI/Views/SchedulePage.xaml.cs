using Microsoft.UI.Xaml;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;


public class SchedulePageBase : ReactivePage<ScheduleViewModel> { }
public sealed partial class SchedulePage : SchedulePageBase
{
    public SchedulePage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            Observable
            .Timer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                foreach (var item in ViewModel.AllAnime)
                {
                    item.RaisePropertyChanged(nameof(AnimeModel.NextEpisodeAt));
                }
            })
            .DisposeWith(d);
        });
    }

    public Visibility IsDayVisible(ScheduleModel model) => model.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
}
