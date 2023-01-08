namespace Totoro.Core.Tests.Helpers;

internal class TestAiredEpisode : AiredEpisode
{
    public TestAiredEpisode()
    {
        Url = Random.Shared.Next().ToString();
    }
}
