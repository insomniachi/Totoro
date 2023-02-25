namespace Totoro.WinUI.Dialogs.ViewModels;

public class ConfigureOptionsViewModel<T> : DialogViewModel
{
    private readonly Action<T, ProviderOptions> _saveFunc;

    [Reactive] public T Type { get; set; }
    [ObservableAsProperty] public ProviderOptions Options { get; }

    public ICommand Save { get; }

    public ConfigureOptionsViewModel(Func<T, ProviderOptions> getOptionsFunc, Action<T, ProviderOptions> saveFunc)
    {
        _saveFunc = saveFunc;

        Save = ReactiveCommand.Create(OnSave);

        this.WhenAnyValue(x => x.Type)
            .WhereNotNull()
            .Select(getOptionsFunc)
            .ToPropertyEx(this, x => x.Options);
    }

    void OnSave() => _saveFunc(Type, Options);
}