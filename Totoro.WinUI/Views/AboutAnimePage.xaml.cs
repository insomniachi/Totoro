using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;


public class AboutAnimePageBase : ReactivePage<AboutAnimeViewModel> { }

public sealed partial class AboutAnimePage : AboutAnimePageBase
{
    public AboutAnimePage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.HasTracking)
                .Subscribe(hasTracking =>
                {
                    if(hasTracking)
                    {
                        EditSymbol.Symbol = Microsoft.UI.Xaml.Controls.Symbol.Edit;
                        EditText.Text = "Update";
                    }
                    else
                    {
                        EditSymbol.Symbol = Microsoft.UI.Xaml.Controls.Symbol.Add;
                        EditText.Text = "Add to list";
                    }    
                })
                .DisposeWith(d);
        });
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if(sender is not ButtonBase b)
        {
            return;
        }

        ViewModel.PlaySound.Execute(b.Tag);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Pause.Execute(null);
    }
}
