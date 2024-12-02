using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Plugins.Anime.Jellyfin;

internal class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
{
	public IAsyncEnumerable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes(int page = 1)
	{
		throw new NotImplementedException();
	}
}
