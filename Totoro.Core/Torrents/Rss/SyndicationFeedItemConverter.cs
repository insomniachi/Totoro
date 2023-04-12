using System.ServiceModel.Syndication;

namespace Totoro.Core.Torrents.Rss
{
    internal class SyndicationItemConverter
    {
        public static RssFeedItem Convert(string url, SyndicationItem item)
        {
            if(url.StartsWith("https://subsplease.org/rss/"))
            {
                return ConvertSubsPlease(item);
            }

            return null;
        }

        private static RssFeedItem ConvertSubsPlease(SyndicationItem item)
        {
            return new MagnetRssFeedItem
            {
                Title = item.Title.Text,
                Magnet = item.Links.FirstOrDefault().Uri.AbsoluteUri
            };
        }
    }
}
