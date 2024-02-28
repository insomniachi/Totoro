using System.Text.Json.Serialization;

namespace Totoro.Plugins.MediaDetection.Vlc.HttpInterface;

internal class VlcStatus
{
    [JsonPropertyName("time")]
    public int Time { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("information")]
    public Information Information { get; set; } = new Information();
}

internal class Information
{
    [JsonPropertyName("category")]
    public Category Category { get; set; } = new();
}

internal class Category
{
    [JsonPropertyName("meta")]
    public Meta Meta { get; set; } = new();
}

internal class Meta
{
    [JsonPropertyName("filename")]
    public string FileName { get; set; } = string.Empty;
}
