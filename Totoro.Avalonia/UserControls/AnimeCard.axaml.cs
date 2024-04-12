using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Totoro.Core.Models;

namespace Totoro.Avalonia.UserControls;

public partial class AnimeCard : UserControl
{
    public AnimeCard()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<bool> ShowNextEpisodeTimeProperty =
        AvaloniaProperty.Register<AnimeCard, bool>(nameof(ShowNextEpisodeTime));

    public static readonly StyledProperty<AnimeModel> AnimeProperty =
        AvaloniaProperty.Register<AnimeCard, AnimeModel>(nameof(Anime));

    public bool ShowNextEpisodeTime
    {
        get => GetValue(ShowNextEpisodeTimeProperty);
        set => SetValue(ShowNextEpisodeTimeProperty, value);
    }

    public AnimeModel Anime
    {
        get => GetValue(AnimeProperty);
        set => SetValue(AnimeProperty, value);
    }

    private void InputElement_OnTapped(object? sender, TappedEventArgs e)
    {
        
    }
}