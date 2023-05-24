using Totoro.Plugins.Options;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class ConfigureOptionsViewModel<T> : DialogViewModel
{
    private readonly Action<T, PluginOptions> _saveFunc;

    [Reactive] public T Type { get; set; }
    [ObservableAsProperty] public PluginOptions Options { get; }

    public ICommand Save { get; }

    public ConfigureOptionsViewModel(Func<T, PluginOptions> getOptionsFunc, Action<T, PluginOptions> saveFunc)
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