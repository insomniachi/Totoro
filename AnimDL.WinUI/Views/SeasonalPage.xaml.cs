using System;
using AnimDL.WinUI.ViewModels;
using MalApi;
using Microsoft.UI.Xaml;
using ReactiveUI;

namespace AnimDL.WinUI.Views;

public class SeasonalPageBase : ReactivePageEx<SeasonalViewModel> { }

public sealed partial class SeasonalPage : SeasonalPageBase
{
    public SeasonalPage()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.ViewModel.Season)
            .WhereNotNull()
            .Subscribe(x =>
            {
                if (x == ViewModel.Current)
                {
                    CurrentFlyoutToggle.IsChecked = true;
                }
                else if (x == ViewModel.Next)
                {
                    NextFlyoutToggle.IsChecked = true;
                }
                else if (x == ViewModel.Prev)
                {
                    PrevFlyoutToggle.IsChecked = true;
                }
            });
    }

    public static Visibility AddToListButtonVisibility(Anime a) => a.UserStatus is null ? Visibility.Visible : Visibility.Collapsed;
}
