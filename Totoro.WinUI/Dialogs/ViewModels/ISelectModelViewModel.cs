namespace Totoro.WinUI.Dialogs.ViewModels;

public interface ISelectModelViewModel
{
    IEnumerable<object> Models { get; set; }
    object SelectedModel { get; set; }
}
