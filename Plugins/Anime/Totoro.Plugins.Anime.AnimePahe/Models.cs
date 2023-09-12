namespace Totoro.Plugins.Anime.AnimePahe;

#pragma warning disable IDE1006 // Naming Styles, interal models, not used anywhere, naming not important.

internal class AnimePaheEpisodePage
{
    public double total { get; set; }
    public int per_page { get; set; }
    public int current_page { get; set; }
    public int last_page { get; set; }
    public string next_page_url { get; set; } = string.Empty;
    public string prev_page_url { get; set; } = string.Empty;
    public int from { get; set; }
    public int to { get; set; }
    public List<AnimePaheEpisodeInfo> data { get; set; } = new();
}

internal class AnimePaheEpisodeInfo
{
    public int id { get; set; }
    public int anime_id { get; set; }
    public double episode { get; set; }
    public double episode2 { get; set; }
    public string edition { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string snapshot { get; set; } = string.Empty;
    public string disc { get; set; } = string.Empty;
    public string duration { get; set; } = string.Empty;
    public string session { get; set; } = string.Empty;
    public int filler { get; set; }
    public string created_at { get; set; } = string.Empty;
}

internal class AnimePaheEpisodeStream
{
    public int id { get; set; }
    public int filesize { get; set; }
    public string crc32 { get; set; } = string.Empty;
    public string revision { get; set; } = string.Empty;
    public string fansub { get; set; } = string.Empty;
    public string audio { get; set; } = string.Empty;
    public string disc { get; set; } = string.Empty;
    public int hq { get; set; }
    public int av1 { get; set; }
    public string kwik { get; set; } = string.Empty;
    public string kwik_pahewin { get; set; } = string.Empty;
}
