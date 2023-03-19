using System.Text.RegularExpressions;
using AnitomySharp;
using CliWrap;
using CliWrap.EventStream;

namespace Totoro.Core.Services.StreamResolvers;

public partial class WebTorrentStreamModelResolver : IVideoStreamModelResolver
{
    private readonly IEnumerable<Element> _parsedResults;
    private readonly string _magnet;

    public WebTorrentStreamModelResolver(IEnumerable<Element> parsedResults, string magnet)
    {
        _parsedResults = parsedResults;
        _magnet = magnet;
    }

    public Task<EpisodeModelCollection> ResolveAllEpisodes(string subStream)
    {
        var epString = _parsedResults.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber)?.Value ?? "1";
        var ep = int.Parse(epString);
        return Task.FromResult(EpisodeModelCollection.FromEpisode(ep));
    }

    public Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream)
    {
        var result = Cli.Wrap("webtorrent").WithArguments(new[] { _magnet, "-s", "0" }).WithWorkingDirectory(@"C:\Users\athul\AppData\Local\Totoro\ApplicationData");

        var mre = new ManualResetEventSlim();
        string url = string.Empty;
        Task.Run(async () =>
        {
            await foreach (var @event in result.ListenAsync())
            {
                if(!string.IsNullOrEmpty(url))
                {
                    continue;
                }

                switch (@event)
                {
                    case StandardOutputCommandEvent stdOut:
                        if (stdOut.Text.Contains("Server running at:"))
                        {
                            url = ServerRegex().Match(stdOut.Text).Groups[1].Value;
                            mre.Set();
                        }
                        break;
                }
            }
        });

        if (string.IsNullOrEmpty(url))
        {
            mre.Wait(); 
        }
        return Task.FromResult(new VideoStreamsForEpisodeModel(url));
    }

    [GeneratedRegex("Server running at:(.+)")]
    private static partial Regex ServerRegex();
}

