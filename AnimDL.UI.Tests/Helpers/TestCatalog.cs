namespace AnimDL.UI.Tests.Helpers;

public abstract class TestCatalog : ICatalog, IMalCatalog
{
    public abstract IAsyncEnumerable<SearchResult> Search(string query);
    public abstract Task<(SearchResult Sub, SearchResult Dub)> SearchByMalId(long id);
}
