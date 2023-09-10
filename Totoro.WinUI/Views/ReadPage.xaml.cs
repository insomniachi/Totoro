using Microsoft.UI.Xaml.Controls;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class ReadPageBase : ReactivePage<ReadViewModel> { }

public sealed partial class ReadPage : ReadPageBase
{
    public ReadPage()
    {
        this.InitializeComponent();
    }
}
