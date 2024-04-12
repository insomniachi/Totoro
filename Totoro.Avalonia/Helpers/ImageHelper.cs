using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Flurl.Http;

namespace Totoro.Avalonia.Helpers;

public static class ImageHelper
{
    public static Bitmap LoadFromResource(Uri resourceUri)
    {
        return new Bitmap(AssetLoader.Open(resourceUri));
    }

    public static async Task<Bitmap?> LoadFromWeb(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }
        
        return await LoadFromWeb(new Uri(url));
    }

    public static async Task<Bitmap?> LoadFromWeb(Uri url)
    {
        try
        {
            var data = await url.GetBytesAsync();
            return new Bitmap(new MemoryStream(data));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while downloading image '{url}' : {ex.Message}");
            return null;
        }
    }
}