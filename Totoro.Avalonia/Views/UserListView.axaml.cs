using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Totoro.Core.ViewModels;

namespace Totoro.Avalonia.Views;

public partial class UserListView : ReactiveUserControl<UserListViewModel>
{
    public UserListView()
    {
        InitializeComponent();
    }
}