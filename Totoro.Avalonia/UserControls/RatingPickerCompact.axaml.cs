using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ReactiveUI;
using Totoro.Core.Models;

namespace Totoro.Avalonia.UserControls;

public partial class RatingPickerCompact : UserControl
{
    public RatingPickerCompact()
    {
        InitializeComponent();
        
        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .SelectMany(x => x.WhenAnyValue(y => y.Tracking))
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(tracking =>
            {
                var text = tracking.Score == 0 ? "-" : tracking.Score.ToString();
                CompactRatingText.Text = text;
            });
    }

    public static readonly StyledProperty<AnimeModel?> AnimeProperty =
        AvaloniaProperty.Register<RatingPickerCompact, AnimeModel?>(nameof(Anime));

    public AnimeModel? Anime
    {
        get => GetValue(AnimeProperty);
        set => SetValue(AnimeProperty, value);
    }

    private void ControlLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not StackPanel s)
        {
            return;
        }
        
        if(FlyoutBase.GetAttachedFlyout(s) is not MenuFlyout flyout)
        {
            return;
        }

        var score = Anime?.Tracking?.Score ?? 0;
        if(score > 10)
        {
            return;
        }

        ((MenuItem)flyout.Items[score]!).IsSelected = true;
    }

    private void RatingTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not StackPanel s)
        {
            return;
        }
        
        FlyoutBase.ShowAttachedFlyout(s);
    }
}