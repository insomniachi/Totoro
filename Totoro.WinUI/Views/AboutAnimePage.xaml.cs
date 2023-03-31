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
                    if (hasTracking)
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
}