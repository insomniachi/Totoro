using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Totoro.Plugins;

#nullable disable

[DebuggerDisplay("{DisplayName}")]
public class PluginInfo
{
    public PluginInfo()
    {
    }

    public string Name { get; init; }
    public string DisplayName { get; init; }
    public Version Version { get; init; }
    public string Description { get; init; }
    public Stream Icon { get; init; }
}

public record PluginInfoSlim(string FileName, string Version, bool Exists = true, string DisplayName = "");

public class PluginInfoEqualityComparer : IEqualityComparer<PluginInfoSlim>
{
    public bool Equals(PluginInfoSlim x, PluginInfoSlim y)
    {
        return x.FileName == y.FileName &&
               x.Version == y.Version;
    }

    public int GetHashCode([DisallowNull] PluginInfoSlim obj)
    {
        return HashCode.Combine(obj.FileName, obj.Version);
    }
}
