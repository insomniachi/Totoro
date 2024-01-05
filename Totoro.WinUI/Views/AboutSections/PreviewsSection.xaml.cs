using Microsoft.UI.Xaml.Controls;
using Totoro.Core.ViewModels.About;

namespace Totoro.WinUI.Views.AboutSections;

public class PreviewsSectionBase : ReactivePage<PreviewsViewModel> { }

public sealed partial class PreviewsSection : PreviewsSectionBase
{
    public PreviewsSection()
    {
        InitializeComponent();
    }

    private void PlayVideo(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        App.Commands.PlayVideo.Execute(args.InvokedItem);
    }
}
