using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Totoro.Avalonia.Helpers;

namespace Totoro.Avalonia.UserControls;

public partial class AnimeCardLarge : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AnimeCardLarge, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> ImageProperty =
        AvaloniaProperty.Register<AnimeCardLarge, string?>(nameof(Image), string.Empty);
    
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<AnimeCardLarge, string>(nameof(Description), string.Empty);
    
    public AnimeCardLarge()
    {
        InitializeComponent();

        // ImageProperty.Changed.AddClassHandler<AnimeCardLarge>( async (card, e) =>
        // {
        //     if (e.NewValue is not string s)
        //     {
        //         return;
        //     }
        //
        //     if (string.IsNullOrEmpty(s))
        //     {
        //         return;
        //     }
        //
        //     ImageControl.Source = await ImageHelper.LoadFromWeb(s);
        // });
    }
    
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
    
    public string? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }
    
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}