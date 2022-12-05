using System.Reflection;
using System.Text.Json;

namespace Totoro.Core.Tests.Helpers;

internal class SnapshotService
{
    public static T GetSnapshot<T>(string key)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Totoro.Core.Tests.Snapshots.{key}.json";
        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        using StreamReader reader = new(stream);
        string jsonFile = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(jsonFile);
    }
}
