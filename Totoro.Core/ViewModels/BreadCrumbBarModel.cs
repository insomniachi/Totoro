namespace Totoro.Core.ViewModels;

public class BreadCrumbBarModel : ReactiveObject
{
    public ObservableCollection<string> BreadCrumbs { get; }
    [ObservableAsProperty] public string State { get; }

    public BreadCrumbBarModel(string root)
    {
        BreadCrumbs = [root];
        BreadCrumbs.ToObservableChangeSet()
            .Select(_ => string.Join(">", BreadCrumbs))
            .ToPropertyEx(this, x => x.State, initialValue: root);
    }
}
