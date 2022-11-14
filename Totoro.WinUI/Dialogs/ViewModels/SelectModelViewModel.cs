namespace Totoro.WinUI.Dialogs.ViewModels;

public class SelectModelViewModel : DialogViewModel, ISelectModelViewModel
{
    [Reactive] public IEnumerable<object> Models { get; set; }
    [Reactive] public object SelectedModel { get; set; }
}
