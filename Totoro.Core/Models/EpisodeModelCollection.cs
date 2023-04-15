using System.ComponentModel;
using System.Runtime.CompilerServices;
using AnitomySharp;
using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Models
{
    public class EpisodeModelCollection : Collection<EpisodeModel>, INotifyPropertyChanged
    {
        private EpisodeModelCollection() { }
        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName] string property = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        private EpisodeModel _current;
        public EpisodeModel Current
        {
            get => _current;
            set
            {
                if (_current == value)
                {
                    return;
                }

                _current = value;
                RaisePropertyChanged();
            }
        }

        public void SelectEpisode(int episode)
        {
            Current = this.FirstOrDefault(x => x.EpisodeNumber == episode);
        }

        public void SelectNext()
        {
            Current = this.FirstOrDefault(x => x.EpisodeNumber == Current.EpisodeNumber + 1);
        }

        public void SelectPrevious()
        {
            Current = this.FirstOrDefault(x => x.EpisodeNumber == Current.EpisodeNumber - 1);
        }


        public static EpisodeModelCollection FromEpisodeCount(int count)
        {
            var collection = new EpisodeModelCollection();
            collection.AddRange(Enumerable.Range(1, count).Select(ep => new EpisodeModel
            {
                EpisodeNumber = ep,
                IsSpecial = false,
                SpecialEpisodeNumber = string.Empty,
                EpisodeTitle = string.Empty,
            }));

            return collection;
        }

        public static EpisodeModelCollection FromEpisode(int episode)
        {
            return new EpisodeModelCollection
            {
                new EpisodeModel
                {
                    EpisodeNumber = episode,
                    IsSpecial = false,
                    SpecialEpisodeNumber = string.Empty,
                    EpisodeTitle = string.Empty,
                }
            };
        }

        public static EpisodeModelCollection FromEpisodes(IEnumerable<int> episodes)
        {
            var collecton = new EpisodeModelCollection();
            collecton.AddRange(episodes.Select(ep => new EpisodeModel
            {
                EpisodeNumber = ep,
                IsSpecial = false,
                SpecialEpisodeNumber = string.Empty,
                EpisodeTitle = string.Empty,
            }));

            return collecton;
        }

        public static EpisodeModelCollection FromEpisode(int start, int end)
        {
            var collecton = new EpisodeModelCollection();
            collecton.AddRange(Enumerable.Range(start, end - start + 1).Select(ep => new EpisodeModel
            {
                EpisodeNumber = ep,
                IsSpecial = false,
                SpecialEpisodeNumber = string.Empty,
                EpisodeTitle = string.Empty,
            }));

            return collecton;
        }

        public static EpisodeModelCollection FromDirectDownloadLinks(IEnumerable<DirectDownloadLink> links)
        {
            var collecton = new EpisodeModelCollection();
            var options = new Options(title: true, extension: false, group: false);
            collecton.AddRange(links.Select(ddl =>
            {
                var title = string.Empty;
                var epString = string.Empty;
                var parsedResult = AnitomySharp.AnitomySharp.Parse(ddl.FileName, options);
                if (parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeTitle) is { } epTitleResult)
                {
                    title = epTitleResult.Value;
                }
                if (parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber) is { } epNumberResult)
                {
                    epString = epNumberResult.Value;
                    if (int.TryParse(epNumberResult.Value, out int ep))
                    {
                        return new EpisodeModel
                        {
                            EpisodeTitle = title,
                            EpisodeNumber = ep,
                            SpecialEpisodeNumber = string.Empty,
                            IsSpecial = false
                        };
                    }
                }
                return new EpisodeModel
                {
                    IsSpecial = true,
                    EpisodeTitle = title,
                    SpecialEpisodeNumber = epString
                };
            }).OrderBy(x => x.EpisodeNumber));

            return collecton;
        }

        public static EpisodeModelCollection Empty { get; } = new EpisodeModelCollection();
    }
}
