namespace Totoro.Core.Services.AniList;

public class AniListAuthToken
{
    public string AccessToken { get; set; }
    public long ExpiresIn { get; set; }
    public DateTime CreatedAt { get; set; }
}
