using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Totoro.WinUI.UserControls;

public sealed class CopyButton : Button
{
    public CopyButton()
    {
        this.DefaultStyleKey = typeof(CopyButton);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetTemplateChild("CopyToClipboardSuccessAnimation") is Storyboard _storyBoard)
        {
            _storyBoard.Begin();
        }
    }

    protected override void OnApplyTemplate()
    {
        Click -= CopyButton_Click;
        base.OnApplyTemplate();
        Click += CopyButton_Click;
    }
}
