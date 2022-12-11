using DynamicData.Operators;

namespace Totoro.Core.ViewModels;

public class PagerViewModel : ReactiveObject
{
    public PagerViewModel(int currentPage, int pageSize)
    {
        CurrentPage = currentPage;
        PageSize = pageSize;

        NextPageCommand = ReactiveCommand.Create(() => ++CurrentPage, this.WhenAnyValue(x => x.CurrentPage).Select(page => page < PageCount - 1));
        PreviousPageCommand = ReactiveCommand.Create(() => --CurrentPage, this.WhenAnyValue(x => x.CurrentPage).Select(page => page > 0));
    }

    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    [Reactive] public int TotalCount { get; set; }
    [Reactive] public int PageCount { get; set; }
    [Reactive] public int CurrentPage { get; set; }
    [Reactive] public int PageSize { get; set; }

    public IObservable<PageRequest> AsPager()
    {
        return this.WhenAnyValue(x => x.CurrentPage, x => x.PageSize)
                   .Select(x => new PageRequest(x.Item1 + 1, x.Item2))
                   .DistinctUntilChanged();
    }

    public void Update(IPageResponse response)
    {
        CurrentPage = response.Page - 1;
        PageSize = response.PageSize;
        PageCount = response.Pages;
        TotalCount = response.TotalSize;
    }
}
