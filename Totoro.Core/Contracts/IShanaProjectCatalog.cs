namespace Totoro.Core.Contracts
{
    public interface IShanaProjectService
    {
        Task<IEnumerable<ShanaProjectCatalogItem>> Search(string term);
        Task<ShanaProjectPage> Search(long Id, int page = 1);
    }
}
