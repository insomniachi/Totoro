namespace Totoro.Core.Contracts
{
    public interface ILocalMediaService
    {
        string GetDirectory();
        long GetId(string directory);
        void SetId(string directory, long id);
    }
}