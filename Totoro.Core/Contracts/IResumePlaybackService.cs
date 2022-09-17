namespace Totoro.Core.Contracts;

public interface IPlaybackStateStorage
{
    double GetTime(long id, int episode);
    void Reset(long id, int episode);
    void Update(long id, int episode, double time);
    void StoreState();
}
