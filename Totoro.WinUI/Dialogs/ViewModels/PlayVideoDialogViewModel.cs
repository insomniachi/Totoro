namespace AnimDL.WinUI.Dialogs.ViewModels
{
    public sealed class PlayVideoDialogViewModel : DialogViewModel, IDisposable
    {
        [Reactive] public string Url { get; set; }

        public void Dispose()
        {
            _close.OnNext(Unit.Default);
        }
    }
}
