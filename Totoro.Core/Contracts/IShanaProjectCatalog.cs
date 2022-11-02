namespace Totoro.Core.Contracts
{
    public interface IShanaProjectService
    {
        Task<IEnumerable<ShanaProjectCatalogItem>> Search(string term);
    }
}
