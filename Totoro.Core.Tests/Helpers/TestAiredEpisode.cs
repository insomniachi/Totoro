namespace Totoro.Core.Tests.Helpers;

internal class TestAiredEpisode : AiredEpisode
{
    private readonly int _episode;

    public override int GetEpisode() => _episode;

    public TestAiredEpisode(int episode)
	{
        _episode = episode;
    }
}
