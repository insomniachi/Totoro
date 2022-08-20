using MalApi;
using Microsoft.UI.Xaml;

namespace AnimDL.WinUI.Views;

public class SeasonalPageBase : ReactivePage<SeasonalViewModel> { }

public sealed partial class SeasonalPage : SeasonalPageBase
{
    public SeasonalPage()
    {
        InitializeComponent();

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
            });
    }

    public static Visibility AddToListButtonVisibility(Anime a) => a.UserStatus is null ? Visibility.Visible : Visibility.Collapsed;
}
