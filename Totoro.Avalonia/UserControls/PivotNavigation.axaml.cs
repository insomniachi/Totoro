using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Avalonia.Contracts;
using Totoro.Core.ViewModels;
using FluentAvalonia.Core;

namespace Totoro.Avalonia.UserControls;

public partial class PivotNavigation : UserControl
{
    private IAvaloniaNavigationService? _navigationService;
    private static readonly NavigationTransitionInfo _fromLeft = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft };
    private static readonly NavigationTransitionInfo _fromRight = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
    private int _prevIndex = 0;
    
    public PivotNavigation()
    {
        InitializeComponent();

        SectionGroupNameProperty.Changed.AddClassHandler<PivotNavigation>((navigation, e) =>
        {
            navigation._navigationService = App.Services.GetRequiredKeyedService<IAvaloniaNavigationService>(e.NewValue as string);
            navigation._navigationService.SetFrame(navigation.NavigationFrame);
        });

        ItemSourceProperty.Changed.AddClassHandler<PivotNavigation>((navigation, e) =>
        {
            if (e.NewValue is not IEnumerable<PivotItemModel> models)
            {
                return;
            }

            var list = models.ToList();
            if (list.Count == 0)
            {
                return;
            }
            
            Navigate(navigation, list[0]);
        });

        SelectedItemProperty.Changed.AddClassHandler<PivotNavigation>((navigation, e) =>
        {
            if(e.NewValue is not PivotItemModel { } selectedItem)
            {
                return;
            }
            
            Navigate(navigation, selectedItem);
        });

        Unloaded += (sender, args) =>
        {
            if(_navigationService is null)
            {
                return;
            }

            _navigationService.Dispose();
        };
    }

    public static readonly StyledProperty<string?> SectionGroupNameProperty =
        AvaloniaProperty.Register<PivotNavigation, string?>(nameof(SectionGroupName));

    public static readonly StyledProperty<IEnumerable<PivotItemModel>> ItemSourceProperty =
        AvaloniaProperty.Register<PivotNavigation, IEnumerable<PivotItemModel>>(nameof(ItemSource), []);

    public static readonly StyledProperty<PivotItemModel?> SelectedItemProperty =
        AvaloniaProperty.Register<PivotNavigation, PivotItemModel?>(nameof(SelectedItem));

    public string? SectionGroupName
    {
        get => GetValue(SectionGroupNameProperty);
        set => SetValue(SectionGroupNameProperty, value);
    }

    public IEnumerable<PivotItemModel> ItemSource
    {
        get => GetValue(ItemSourceProperty);
        set => SetValue(ItemSourceProperty, value);
    }

    public PivotItemModel? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void Navigate(PivotNavigation navigation, PivotItemModel selectedItem)
    {
        if (navigation._navigationService is null)
        {
            return;
        }
        
        if(!navigation._navigationService.HasFrame())
        {
            navigation._navigationService.SetFrame(navigation.NavigationFrame);
        }

        var index = navigation.ItemSource.IndexOf(selectedItem);   
        
        var transition = navigation._prevIndex == index
            ? new SuppressNavigationTransitionInfo()
            : index > navigation._prevIndex
                ? _fromRight 
                : _fromLeft;

        navigation._prevIndex = index;
        navigation._navigationService.NavigateTo(selectedItem.ViewModel, parameter: selectedItem.NavigationParameters, transitionInfo: transition);
    }
}