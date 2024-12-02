namespace Totoro.Plugins.Anime.Jellyfin;

public class Item
{
	public string Name { get; set; }
	public string ServerId { get; set; }
	public string Id { get; set; }
	public DateTime PremiereDate { get; set; }
	public string OfficialRating { get; set; }
	public string ChannelId { get; set; }
	public double CommunityRating { get; set; }
	public long RunTimeTicks { get; set; }
	public int ProductionYear { get; set; }
	public bool IsFolder { get; set; }
	public string Type { get; set; }
	public UserData UserData { get; set; }
	public string Status { get; set; }
	public List<string> AirDays { get; set; } = new();
	public ImageTags ImageTags { get; set; }
	public List<string> BackdropImageTags { get; set; } = new();
	public ImageBlurHashes ImageBlurHashes { get; set; }
	public string LocationType { get; set; }
	public string MediaType { get; set; }
	public DateTime EndDate { get; set; }
	public int IndexNumber { get; set; }
}

public class UserData
{
	public int UnplayedItemCount { get; set; }
	public long PlaybackPositionTicks { get; set; }
	public int PlayCount { get; set; }
	public bool IsFavorite { get; set; }
	public bool Played { get; set; }
	public string Key { get; set; }
	public string ItemId { get; set; }
}

public class ImageTags
{
	public string Primary { get; set; }
	public string Logo { get; set; }
	public string Thumb { get; set; }
}

public class ImageBlurHashes
{
	public Dictionary<string, string> Backdrop { get; set; } = new();
	public Dictionary<string, string> Primary { get; set; } = new();
	public Dictionary<string, string> Logo { get; set; } = new();
	public Dictionary<string, string> Thumb { get; set; } = new(); 
}

public class Root
{
	public List<Item> Items { get; set; } = new();
	public int TotalRecordCount { get; set; }
	public int StartIndex { get; set; }
}

