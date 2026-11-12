using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.Helpers;

namespace Totoro.Agent;

[Description("An anime an image is from, and where in it.")]
public sealed record ImageAnimeMatch(
	AnimeSummary Anime,
	[property: Description("The episode the frame is from, if known.")]
	int? Episode,
	[property: Description("When in the episode the frame is, e.g. \"14:17\".")]
	string At,
	[property: Description("How alike the image and the frame are, out of 100.")]
	int Similarity,
	[property: Description("Where to start the episode to see the scene (a little before it), in seconds: play_episode's startAtSeconds.")]
	int PlayFromSeconds) : IAgentAnime
{
	AnimeModel? IAgentAnime.Source => ((IAgentAnime)Anime).Source;
}

/// <summary>Agent tools for finding which anime a screenshot is from (see <see cref="ImageSearchService"/>).</summary>
public sealed class ImageSearchTools(ImageSearchService search)
{
	[AgentTool("find_anime_from_clipboard_image")]
	[Description("Finds which anime, episode and moment the image the user copied (e.g. a screenshot) is from. Best match first; empty if none is close.")]
	public async Task<IReadOnlyList<ImageAnimeMatch>> FindFromClipboardAsync()
	{
		if (await ClipboardImage.OpenAsync() is not var (image, contentType))
		{
			throw new AgentToolException("There's no image in the clipboard. Ask the user to copy one.");
		}

		using (image)
		{
			try
			{
				return [.. (await search.FindAsync(image, contentType)).Select(x => new ImageAnimeMatch(
					AnimeSummary.From(x.Anime), x.Episode, x.At.ToString(x.At.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss", System.Globalization.CultureInfo.InvariantCulture),
					(int)Math.Round(x.Similarity * 100), (int)x.PlayFrom.TotalSeconds))];
			}
			catch (InvalidOperationException ex)
			{
				throw new AgentToolException(ex.Message);
			}
		}
	}
}
