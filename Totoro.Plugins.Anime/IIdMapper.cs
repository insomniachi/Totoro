using System.Net.Http;
using System.Text.RegularExpressions;
using Flurl.Http;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime;

public interface IIdMapper
{
    Task<AnimeId> MapId(string url);
}

public static class IdMappingHelper
{
    public static async Task<long?> GetIdFromUrl(string url, Regex regex)
    {
        try
        {
            var html = await url.GetStringAsync();
            var match = regex.Match(html);
            return match.Success ? long.Parse(match.Groups[1].Value) : 0;
        }
        catch
        {
            return null;
        }
    }
}