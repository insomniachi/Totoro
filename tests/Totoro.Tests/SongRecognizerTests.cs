using System.Text;
using Totoro.Core.Services;
using Totoro.Playback;
using Totoro.ViewModels;
using Xunit;

namespace Totoro.Tests;

public class SongRecognizerTests
{
	// Shazam's response for Demon Slayer's first opening, cut down to what's read.
	private const string Match = """
		{"results":{"matches":[{"id":"464452377","type":"shazam-songs"}],"retry":{"recordingIntermissionInMilliseconds":0}},
		 "resources":{"shazam-songs":{"464452377":{"id":"464452377","type":"shazam-songs",
		   "attributes":{"type":"MUSIC","title":"Gurenge","artist":"LiSA","webUrl":"https://www.shazam.com/track/464452377/gurenge"}}}}}
		""";

	// Not heard yet: send more.
	private const string NoMatch = """{"results":{"matches":[],"retry":{"retryInMilliseconds":6000}},"resources":{}}""";

	[Fact]
	public void Match_gives_the_title_and_artist()
	{
		Assert.Equal((("Gurenge", "LiSA"), 0), SongRecognizer.ParseMatch(Match));
	}

	[Fact]
	public void No_match_gives_how_much_to_send_next()
	{
		Assert.Equal((null, 6000), SongRecognizer.ParseMatch(NoMatch));
	}

	[Fact]
	public void Signature_has_its_header_crc_and_peaks()
	{
		// 5 s of notes, each a quarter second that swells and fades (a steady tone has no peak in time).
		var rate = ShazamSignature.SampleRate;
		var random = new Random(1);
		var samples = new float[5 * rate];
		double freq = 0;
		for (var i = 0; i < samples.Length; i++)
		{
			freq = i % (rate / 4) == 0 ? 300 + random.NextDouble() * 3000 : freq;
			samples[i] = (float)(0.5 * Math.Sin(Math.PI * (i % (rate / 4)) / (rate / 4)) * Math.Sin(2 * Math.PI * freq * i / rate));
		}

		var signature = new ShazamSignature();
		signature.Add(samples);
		var bytes = signature.Write();

		Assert.Equal(0xCAFE2580, BitConverter.ToUInt32(bytes, 0));
		Assert.Equal(ShazamSignature.Crc32(bytes.AsSpan(8)), BitConverter.ToUInt32(bytes, 4));
		Assert.Equal(bytes.Length - 48, BitConverter.ToInt32(bytes, 8));
		Assert.Equal(samples.Length, BitConverter.ToInt32(bytes, 40));
		// Four bands' headers (8 bytes each) and some peaks in them.
		Assert.True(bytes.Length > 48 + 4 * 8 + 40, $"Only {bytes.Length} bytes");
	}

	[Fact]
	public void Crc32_is_the_standard_one()
	{
		Assert.Equal(0xCBF43926, ShazamSignature.Crc32(Encoding.ASCII.GetBytes("123456789")));
	}

	[Theory]
	[InlineData("紅蓮の弓矢", "Guren no Yumiya")]
	[InlineData("Gurenge!", "gurenge")]
	[InlineData("crossing field", "Crossing Field")]
	public void Titles_compare_romanised_and_by_letters_alone(string heard, string animeThemes)
	{
		Assert.Equal(AnimeSongService.NormaliseTitle(animeThemes), AnimeSongService.NormaliseTitle(MainWindowViewModel.Romanise(heard)));
	}

	// Made-up, in AnimeThemes' GraphQL artist search layout.
	private const string ArtistSearch = """
		{"data":{"search":{"artists":[{"performances":[
		  {"song":{"animethemes":[{"type":"OP","sequence":1,"song":{"title":{"romaji":"Gurenge"},"performances":[{"artist":{"name":{"main":"LiSA"}}}]},
		    "animethemeentries":[],"anime":{"title":{"romaji":"Kimetsu no Yaiba"},"resources":{"nodes":[{"site":"MAL","externalId":38000}]},"images":{"nodes":[]}}}]}},
		  {"song":{"animethemes":[]}},
		  {"song":{"animethemes":[{"type":"ED","sequence":null,"song":{"title":{"romaji":"oath sign"},"performances":[]},
		    "animethemeentries":[],"anime":{"title":{"romaji":"Fate/Zero"},"resources":{"nodes":[]},"images":{"nodes":[]}}}]}}
		]}]}}}
		""";

	[Fact]
	public void Artist_search_gives_each_of_their_songs_with_its_anime()
	{
		var matches = AnimeSongService.ParseSongMatches(ArtistSearch);

		Assert.Equal(["Gurenge", "oath sign"], matches.Select(x => x.Song.Title));
		Assert.Equal("Kimetsu no Yaiba", matches[0].Anime.Title);
		Assert.Equal(38000L, matches[0].Anime.ExternalIds.MyAnimeList);
		Assert.Equal(AnimeSongKind.Ending, matches[1].Song.Kind);
	}
}
