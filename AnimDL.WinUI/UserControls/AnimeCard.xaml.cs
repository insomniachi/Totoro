using System;
using System.Collections.Generic;
using System.Windows.Input;
using AnimDL.WinUI.Contracts;
using MalApi;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveUI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AnimDL.WinUI.UserControls;

public sealed partial class AnimeCard : UserControl
{
    public static readonly DependencyProperty AnimeProperty =
        DependencyProperty.Register("Anime", typeof(Anime), typeof(AnimeCard), new PropertyMetadata(null));
    private readonly IViewService _viewService = App.GetService<IViewService>();

    public Anime Anime
    {
        get { return (Anime)GetValue(AnimeProperty); }
        set { SetValue(AnimeProperty, value); }
    }

    public MenuFlyout Flyout { get; set; }
    public ICommand UpdateStatusCommand { get; }
    
    public AnimeCard()
    {
        InitializeComponent();
        UpdateStatusCommand = ReactiveCommand.CreateFromTask<Anime>(_viewService.UpdateAnimeStatus);
    }

    public Visibility AddToListButtonVisibility(Anime a)
    {
        if(a is null)
        {
            return Visibility.Collapsed;
        }

        return a.UserStatus is null ? Visibility.Visible : Visibility.Collapsed;
    }

}
