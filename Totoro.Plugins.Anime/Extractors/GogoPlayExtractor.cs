using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime;

public partial class GogoPlayExtractor
{
    [GeneratedRegex("(?:container|videocontent)-(\\d+)", RegexOptions.Compiled)]
    private static partial Regex KeysRegex();

    [GeneratedRegex("data-value=\"(.+?)\"", RegexOptions.Compiled)]
    private static partial Regex EncryptionRegex();

    public static async Task<VideoStreamsForEpisode> Extract(string url)
    {
        var uri = new Url(url);
        var id = uri.QueryParams.FirstOrDefault("id").ToString()!;
        var nextHost = $"https://{uri.Host}/";
        var content = await url.GetStringAsync();

        var matches = KeysRegex().Matches(content);
        var encryptionKey = matches[0].Groups[1].Value;
        var iv = matches[1].Groups[1].Value;
        var decryptionKey = matches[2].Groups[1].Value;
        var encryptedData = EncryptionRegex().Match(content).Groups[1].Value;

        var decrypted = Decrypt(encryptedData, encryptionKey, iv);
        var newUrl = $"{decrypted}&id={Encrypt(id, encryptionKey, iv)}&alias={id}";
        var component = newUrl.Split("&", 2)[1];
        var ajaxUrl = $"{nextHost}encrypt-ajax.php?{component}";

        var response = await ajaxUrl.WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest").GetStringAsync();

        var data = JsonNode.Parse(response)!.AsObject()["data"]!.ToString();
        var decryptedData = Decrypt(data, decryptionKey, iv);
        var jsonData = JsonNode.Parse(decryptedData)!.AsObject();

        var streamForEp = new VideoStreamsForEpisode();

        foreach (var item in jsonData["source"]!.AsArray())
        {
            var resolution = ResolutionRegex().Match(item!["label"]!.ToString()).Groups[1].Value;
            resolution = string.IsNullOrEmpty(resolution) ? "default" : resolution;
            streamForEp.Streams.Add(new VideoStream
            {
                Resolution = resolution,
                Headers = new() { [HeaderNames.Referer] = nextHost },
                Url = item!["file"]!.ToString()
            });
        }

        return streamForEp;
    }

    private static string Encrypt(string data, string key, string iv)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;
        var input = Encoding.UTF8.GetBytes(data);
        using var encryptor = aes.CreateEncryptor();
        var result = encryptor.TransformFinalBlock(input, 0, input.Length);
        return Convert.ToBase64String(result);
    }

    private static string Decrypt(string data, string key, string iv)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);
        aes.Padding = PaddingMode.PKCS7;
        var input = Convert.FromBase64String(data);
        using var decryptor = aes.CreateDecryptor();
        var result = decryptor.TransformFinalBlock(input, 0, input.Length);
        return Encoding.UTF8.GetString(result);
    }

    [GeneratedRegex("(\\d+) P")]
    private static partial Regex ResolutionRegex();
}
