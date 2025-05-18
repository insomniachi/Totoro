namespace Totoro.Plugins.Anime.Models
{
	public class VideoSource
	{
		public string? Title { get; set; }
		public string? Quality { get; init; }
		public required Uri Url { get; init; }
		public Dictionary<string, string> Headers { get; } = [];
	}

	public class VideoServer
	{
		public required string Name { get; init; }
		public required Uri Url { get; init; }
	}

	public class Episode
	{
		public required string Id { get; init; }
		public string? Name { get; init; }
		public float Number { get; init; }
		public Uri? Image { get; init; }
	}
}
