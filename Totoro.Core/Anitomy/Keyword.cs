
namespace Anitomy;


/// <summary>
/// A class to manager the list of known anime keywords. This class is analogous to <code>keyword.cpp</code> of Anitomy, and <code>KeywordManager.java</code> of AnitomyJ
/// </summary>
public static class KeywordManager
{
    private static readonly Dictionary<string, Keyword> Keys = [];
    private static readonly Dictionary<string, Keyword> Extensions = [];
    private static readonly List<(ElementCategory, List<string>)> PeekEntries;

    static KeywordManager()
    {
        var optionsDefault = new KeywordOptions();
        var optionsInvalid = new KeywordOptions(true, true, false);
        var optionsUnidentifiable = new KeywordOptions(false, true, true);
        var optionsUnidentifiableInvalid = new KeywordOptions(false, true, false);
        var optionsUnidentifiableUnsearchable = new KeywordOptions(false, false, true);

        Add(ElementCategory.AnimeSeasonPrefix, optionsUnidentifiable, ["SAISON", "SEASON"]);

        Add(ElementCategory.AnimeType, optionsUnidentifiable, ["GEKIJOUBAN", "MOVIE", "OAD", "OAV", "ONA", "OVA", "SPECIAL", "SPECIALS", "TV"]);

        Add(ElementCategory.AnimeType, optionsUnidentifiableUnsearchable, ["SP"]); // e.g. "Yumeiro Patissiere SP Professional"

        Add(ElementCategory.AnimeType, optionsUnidentifiableInvalid, ["ED", "ENDING", "NCED", "NCOP", "OP", "OPENING", "PREVIEW", "PV"]);

        Add(ElementCategory.AudioTerm, optionsDefault,
            [
                // Audio channels
                "2.0CH", "2CH", "5.1", "5.1CH", "DTS", "DTS-ES", "DTS5.1","TRUEHD5.1",
                // Audio codec
                "AAC", "AACX2", "AACX3", "AACX4", "AC3", "EAC3", "E-AC-3",
                "FLAC", "FLACX2", "FLACX3", "FLACX4", "LOSSLESS", "MP3", "OGG", "VORBIS",
                // Audio language
                "DUALAUDIO", "DUAL AUDIO"
            ]
        );

        Add(ElementCategory.DeviceCompatibility, optionsDefault, ["IPAD3", "IPHONE5", "IPOD", "PS3", "XBOX", "XBOX360"]);

        Add(ElementCategory.DeviceCompatibility, optionsUnidentifiable, ["ANDROID"]);

        Add(ElementCategory.EpisodePrefix, optionsDefault, ["EP", "EP.", "EPS", "EPS.", "EPISODE", "EPISODE.", "EPISODES", "CAPITULO", "EPISODIO", "FOLGE"]);

        Add(ElementCategory.EpisodePrefix, optionsInvalid, ["E", "\\x7B2C"]); // single-letter episode keywords are not valid tokens

        Add(ElementCategory.FileExtension, optionsDefault, ["3GP", "AVI", "DIVX", "FLV", "M2TS", "MKV", "MOV", "MP4", "MPG", "OGM", "RM", "RMVB", "TS", "WEBM", "WMV"]);

        Add(ElementCategory.FileExtension, optionsInvalid, ["AAC", "AIFF", "FLAC", "M4A", "MP3", "MKA", "OGG", "WAV", "WMA", "7Z", "RAR", "ZIP", "ASS", "SRT"]);

        Add(ElementCategory.Language, optionsDefault, ["ENG", "ENGLISH", "ESPANO", "JAP", "PT-BR", "SPANISH", "VOSTFR"]);

        Add(ElementCategory.Language, optionsUnidentifiable, ["ESP", "ITA"]); // e.g. "Tokyo ESP:, "Bokura ga Ita"

        Add(ElementCategory.Other, optionsDefault, ["REMASTER", "REMASTERED", "UNCENSORED", "UNCUT", "TS", "VFR", "WIDESCREEN", "WS"]);

        Add(ElementCategory.ReleaseGroup, optionsDefault, ["THORA"]);

        Add(ElementCategory.ReleaseInformation, optionsDefault, ["BATCH", "COMPLETE", "PATCH", "REMUX"]);

        Add(ElementCategory.ReleaseInformation, optionsUnidentifiable, ["END", "FINAL"]); // e.g. "The End of Evangelion", 'Final Approach"

        Add(ElementCategory.ReleaseVersion, optionsDefault, ["V0", "V1", "V2", "V3", "V4"]);

        Add(ElementCategory.Source, optionsDefault, ["BD", "BDRIP", "BLURAY", "BLU-RAY", "DVD", "DVD5", "DVD9", "DVD-R2J", "DVDRIP", "DVD-RIP", "R2DVD", "R2J", "R2JDVD", "R2JDVDRIP", "HDTV", "HDTVRIP", "TVRIP", "TV-RIP", "WEBCAST", "WEBRIP"]);

        Add(ElementCategory.Subtitles, optionsDefault, ["ASS", "BIG5", "DUB", "DUBBED", "HARDSUB", "HARDSUBS", "RAW", "SOFTSUB", "SOFTSUBS", "SUB", "SUBBED", "SUBTITLED"]);

        Add(ElementCategory.VideoTerm,optionsDefault,
            [
              // Frame rate
              "23.976FPS", "24FPS", "29.97FPS", "30FPS", "60FPS", "120FPS",
              // Video codec
              "8BIT", "8-BIT", "10BIT", "10BITS", "10-BIT", "10-BITS",
              "HI10", "HI10P", "HI444", "HI444P", "HI444PP",
              "H264", "H265", "H.264", "H.265", "X264", "X265", "X.264",
              "AVC", "HEVC", "HEVC2", "DIVX", "DIVX5", "DIVX6", "XVID",
              // Video format
              "AVI", "RMVB", "WMV", "WMV3", "WMV9",
              // Video quality
              "HQ", "LQ",
              // Video resolution
              "HD", "SD"
            ]
      );

        Add(ElementCategory.VolumePrefix,optionsDefault, ["VOL", "VOL.", "VOLUME"]);

        PeekEntries =
        [
            (ElementCategory.AudioTerm, new List<string> { "Dual Audio" }),
            (ElementCategory.VideoTerm, new List<string> { "H264", "H.264", "h264", "h.264" }),
            (ElementCategory.VideoResolution, new List<string> { "480p", "720p", "1080p" }),
            (ElementCategory.Source, new List<string> { "Blu-Ray" })
        ];
    }

    public static string Normalize(string word)
    {
        return string.IsNullOrEmpty(word) ? word : word.ToUpperInvariant();
    }

    public static bool Contains(ElementCategory category, string keyword)
    {
        var keys = GetKeywordContainer(category);
        if (keys.TryGetValue(keyword, out var foundEntry))
        {
            return foundEntry.Category == category;
        }

        return false;
    }

    /// <summary>
    /// Finds a particular <code>keyword</code>. If found sets <code>category</code> and <code>options</code> to the found search result.
    /// </summary>
    /// <param name="keyword">the keyword to search for</param>
    /// <param name="category">the reference that will be set/changed to the found keyword category</param>
    /// <param name="options">the reference that will be set/changed to the found keyword options</param>
    /// <returns>if the keyword was found</returns>
    public static bool FindAndSet(string keyword, ref ElementCategory category, ref KeywordOptions options)
    {
        var keys = GetKeywordContainer(category);
        if (!keys.TryGetValue(keyword, out var foundEntry))
        {
            return false;
        }

        if (category == ElementCategory.Unknown)
        {
            category = foundEntry.Category;
        }
        else if (foundEntry.Category != category)
        {
            return false;
        }
        options = foundEntry.Options;
        return true;
    }

    /// <summary>
    /// Given a particular <code>filename</code> and <code>range</code> attempt to preidentify the token before we attempt the main parsing logic
    /// </summary>
    /// <param name="filename">the filename</param>
    /// <param name="range">the search range</param>
    /// <param name="elements">elements array that any pre-identified elements will be added to</param>
    /// <param name="preidentifiedTokens">elements array that any pre-identified token ranges will be added to</param>
    public static void PeekAndAdd(string filename, TokenRange range, List<Element> elements, List<TokenRange> preidentifiedTokens)
    {
        var endR = range.Offset + range.Size;
        var search = filename.Substring(range.Offset, endR > filename.Length ? filename.Length - range.Offset : endR - range.Offset);
        foreach (var entry in PeekEntries)
        {
            foreach (var keyword in entry.Item2)
            {
                var foundIdx = search.IndexOf(keyword, StringComparison.CurrentCulture);
                if (foundIdx == -1)
                {
                    continue;
                }

                foundIdx += range.Offset;
                elements.Add(new Element(entry.Item1, keyword));
                preidentifiedTokens.Add(new TokenRange(foundIdx, keyword.Length));
            }
        }
    }

    // Private API

    /** Returns the appropriate keyword container. */
    private static Dictionary<string, Keyword> GetKeywordContainer(ElementCategory category)
    {
        return category == ElementCategory.FileExtension ? Extensions : Keys;
    }

    /// Adds a <code>category</code>, <code>options</code>, and <code>keywords</code> to the internal keywords list.
    private static void Add(ElementCategory category, KeywordOptions options, IEnumerable<string> keywords)
    {
        var keys = GetKeywordContainer(category);
        foreach (var key in keywords.Where(k => !string.IsNullOrEmpty(k) && !keys.ContainsKey(k)))
        {
            keys[key] = new Keyword(category, options);
        }
    }
}

/// <summary>
/// Keyword options for a particular keyword.
/// </summary>
/// <remarks>
/// Constructs a new keyword options
/// </remarks>
/// <param name="identifiable">if the token is identifiable</param>
/// <param name="searchable">if the token is searchable</param>
/// <param name="valid">if the token is valid</param>
public class KeywordOptions(bool identifiable, bool searchable, bool valid)
{
    public bool Identifiable { get; } = identifiable;
    public bool Searchable { get; } = searchable;
    public bool Valid { get; } = valid;

    public KeywordOptions() : this(true, true, true) { }
}

/// <summary>
/// A Keyword
/// </summary>
public record Keyword(ElementCategory Category, KeywordOptions Options);
