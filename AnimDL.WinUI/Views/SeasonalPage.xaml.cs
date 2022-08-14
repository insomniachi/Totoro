using System;
using AnimDL.WinUI.ViewModels;
using MalApi;
using Microsoft.UI.Xaml;
using ReactiveUI;

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
                if (season == ViewModel.Current)
                {
                    CurrentFlyoutToggle.IsChecked = true;
                }
                else if (season == ViewModel.Next)
                {
                    NextFlyoutToggle.IsChecked = true;
                }
                else if (season == ViewModel.Prev)
                {
                    PrevFlyoutToggle.IsChecked = true;
                }
            });
    }

    public static Visibility AddToListButtonVisibility(Anime a) => a.UserStatus is null ? Visibility.Visible : Visibility.Collapsed;
}
