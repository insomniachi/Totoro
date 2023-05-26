using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flurl;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.Zoro.Tests;

[ExcludeFromCodeCoverage]
public class StreamProviderTests
{
    public const string Hyouka = "hyouka";

    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };
    private readonly Dictionary<string, string> _urlMap = new()
    {
        { Hyouka, Url.Combine(Config.Url, "/hyouka-349") }
    };
    private readonly bool _allEpisodes = false;

    public StreamProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(Hyouka, 22)]
    public async Task GetNumberOfEpisodes(string key, int expected)
    {        
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var actual = await sut.GetNumberOfStreams(url);

        // assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(Hyouka)]
    public async Task GetStreams(string key)
    {
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var result = await sut.GetStreams(url, _allEpisodes ? Range.All : 1..1).ToListAsync();

        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, item.GetType(), _searializerOption));
        }
    }

    [Fact]
    public void Decryption()
    {
        var secret = Encoding.UTF8.GetBytes("SwG1irHxziGrNQP3kM9d8iGE");
        var salt = BitConverter.ToString(secret).Replace("-", "").ToLower();
        var enryptedUrl = "U2FsdGVkX19Nd/lOvmNSb0NKyYZ7O/ZlCBkB60T+Mu/gACOa4s7SAFJNJpxm+kK7+Rpi9kVtIh5jtzNDBWYuzfqxOpXfyeYpEOxwImsxxUQHSt2SzsHoIpBwwmzoNDzVmbPZO1Bnxsm9qxUebLUxGYKhIRcxyGnO6TN3NGrd8/i5K/X8eGB4DDAzOw/apDegFqgC9mkWFlwXb2HVPcFtds6YYfojhHqWeaMmGcj7riie3/BBHyZdKRN9ooX4AJS5hwn+gA3NDZ57I3G0MvfA0K2w3f3rjwmKtYwtXMv0IC6jU7X1ZeOxBKzZLcUUzM8q/i/mPIBd1oEH8oygHqj/dgsiPe+pllROLU61XOibelbvEJ4EvEFQRpSZlICC5mUZ9r4+224+s4rU5N6Swp4Oc0z4yfW9Vjz4sSSrdgbZWkXIG8H8f3/1VlMVZMoE7pPx6JuOxq0D0eCuTSwNzVEYOA==";
        var rawValue = Convert.FromBase64String(enryptedUrl);
        var rawValueString = BitConverter.ToString(rawValue).Replace("-","").ToLower();
        var rawValueStringSliced = BitConverter.ToString(rawValue[8..16]).Replace("-", "").ToLower();
        var keyString = BitConverter.ToString(GenerateKeyFromSalt(rawValue[8..16], secret)).Replace("-", "").ToLower();
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
