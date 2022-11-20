using MalApi;
using Microsoft.UI.Xaml;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class SeasonalPageBase : ReactivePage<SeasonalViewModel> { }

public sealed partial class SeasonalPage : SeasonalPageBase
{
    public SeasonalPage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.Season)
                .WhereNotNull()
                .Subscribe(season =>
                {
                    if (season == SeasonalViewModel.Current)
                    {
                        CurrentFlyoutToggle.IsChecked = true;
                    }
                    else if (season == SeasonalViewModel.Next)
                    {
                        NextFlyoutToggle.IsChecked = true;
                    }
                    else if (season == SeasonalViewModel.Prev)
                    {
                        PrevFlyoutToggle.IsChecked = true;
                    }
                })
                .DisposeWith(d);

            this.WhenAnyValue(x => x.ViewModel.Sort)
                .Subscribe(sort =>
                {
                    if (sort == Sort.Popularity)
                    {
                        PopularityRadio.IsChecked = true;
                    }
                    else if (sort == Sort.Score)
                    {
                        ScoreRadio.IsChecked = true;
                    }
                    ViewModel.RefreshData();
                })
                .DisposeWith(d);
        });
    }

    public static Visibility AddToListButtonVisibility(Anime a) => a.UserStatus is null ? Visibility.Visible : Visibility.Collapsed;
}
