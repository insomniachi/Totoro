using ReactiveMarbles.ObservableEvents;
using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Dialogs.Views;

public class UpdateAnimeStatusViewBase : ReactiveContentDialog<UpdateAnimeStatusViewModel> { }

public sealed partial class UpdateAnimeStatusView : UpdateAnimeStatusViewBase
{
    public List<AnimeStatus> Statuses { get; } = Enum.GetValues<AnimeStatus>().Cast<AnimeStatus>().SkipLast(1).ToList();
    public UpdateAnimeStatusView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.StartDate)
                .WhereNotNull()
                .Select(x => x.Value)
                .Subscribe(date => StartDate.Date = date)
                .DisposeWith(d);

            this.WhenAnyValue(x => x.ViewModel.FinishDate)
                .WhereNotNull()
                .Select(x => x.Value)
                .Subscribe(date => FinishDate.Date = date)
                .DisposeWith(d);

            StartDate
            .Events()
            .SelectedDateChanged
            .Select(x => x.args.NewDate)
            .WhereNotNull()
            .Select(x => x.Value)
            .Subscribe(date => ViewModel.StartDate = date);

            FinishDate
            .Events()
            .SelectedDateChanged
            .Select(x => x.args.NewDate)
            .WhereNotNull()
            .Select(x => x.Value)
            .Subscribe(date => ViewModel.StartDate = date);

        });
    }
}
