using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DynamicData;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.Extractors;

public static class RapidVideoExtractor
{
    public const string SALT_SECRET_ENDPOINT = "https://github.com/enimax-anime/key/raw/e6/key.txt";

    public static async Task<VideoStreamsForEpisode?> Extract(string url)
    {
        var urlObj = new Url(url);
        var ajaxResponse = await $"https://{urlObj.Host}/ajax/embed-6/getSources"
            .SetQueryParam("id", urlObj.PathSegments.Last())
            .GetStringAsync();

        var json = JsonNode.Parse(ajaxResponse);

        var encrypted = json!["encrypted"]!.GetValue<bool>();
        if(encrypted)
        {
            var stream = new VideoStreamsForEpisode();
            var saltSecret = Encoding.UTF8.GetBytes(await SALT_SECRET_ENDPOINT.GetStringAsync());
            var items = JsonNode.Parse(DecipherSaltedAes(json!["sources"]!.ToString(), saltSecret))!.AsArray();
            items.AddRange(JsonNode.Parse(DecipherSaltedAes(json!["sourcesBackup"]!.ToString(), saltSecret))!.AsArray());
            foreach (var item in items)
            {
                stream.Streams.Add(new VideoStream
                {
                    Resolution = "default",
                    Url = item!["file"]!.ToString()
                });
            }

            foreach (var item in json!["tracks"]!.AsArray())
            {
                if (item!["kind"]!.ToString() != "captions")
                {
                    continue;
                }

                var file = item!["file"]!.ToString();
                var label = item!["label"]!.ToString();
                stream.AdditionalInformation.Subtitles.Add(new Subtitle(file, label));

                return stream;
            }
        }

        return null;
    }

    private static string DecipherSaltedAes(string encoded, byte[] secret)
    {
        var rawValue = Convert.FromBase64String(encoded);
        var key = GenerateKeyFromSalt(rawValue[8..16], secret);

        using var aes = Aes.Create();
        aes.Key = key.Take(32).ToArray();
        aes.IV = key.Skip(32).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var input = Convert.FromBase64String(encoded).Skip(16).ToArray();
        using var decryptor = aes.CreateDecryptor();
        var result = decryptor.TransformFinalBlock(input, 0, input.Length);
        return Encoding.UTF8.GetString(result);
    }

    private static byte[] GenerateKeyFromSalt(byte[] salt, byte[] secret)
    {
        var key = MD5.HashData(Combine(secret, salt));
        var currentKey = key;

        while (currentKey.Length < 48)
        {
            key = MD5.HashData(Combine(key, secret, salt));
            currentKey = Combine(currentKey, key);
        }

        return currentKey.Take(48).ToArray();
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        byte[] rv = new byte[arrays.Sum(a => a.Length)];
        int offset = 0;
        foreach (byte[] array in arrays)
        {
            Buffer.BlockCopy(array, 0, rv, offset, array.Length);
            offset += array.Length;
        }
        return rv;
    }
}
