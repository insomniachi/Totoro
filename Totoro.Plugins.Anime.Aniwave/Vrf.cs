using System.Text.Json.Nodes;
using Flurl.Http;

namespace Totoro.Plugins.Anime.Aniwave;

internal static class Vrf
{
    internal static async Task<string> Encode(string text)
    {
        var response = await $"https://9anime.eltik.net/vrf?query={text}&apikey=saikou".GetStringAsync();
        var jObject = JsonNode.Parse(response);
        return jObject!["url"]!.ToString();
    }

    internal static async Task<string> Decode(string text)
    {
        var response = await $"https://9anime.eltik.net/decrypt?query={text}&apikey=saikou".GetStringAsync();
        var jObject = JsonNode.Parse(response);
        return jObject!["url"]!.ToString();
    }
}
