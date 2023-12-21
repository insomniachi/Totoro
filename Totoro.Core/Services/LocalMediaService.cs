namespace Totoro.Core.Services
{
    public class LocalMediaService : ILocalMediaService
    {
        private readonly ILocalSettingsService _localSettingsService;
        private readonly Dictionary<string, long> _dirToIdlookup;
        private readonly Dictionary<long, string> _idToDirLookup;

        public LocalMediaService(ILocalSettingsService localSettingsService)
        {
            _localSettingsService = localSettingsService;
            _dirToIdlookup = _localSettingsService.ReadSetting<Dictionary<string, long>>("LocalMedia", []);
            _idToDirLookup = _dirToIdlookup.ToDictionary(x => x.Value, x => x.Key);
        }

        public void SetId(string directory, long id)
        {
            _dirToIdlookup[directory] = id;
            _localSettingsService.SaveSetting("LocalMedia", _dirToIdlookup);
        }

        public long GetId(string directory)
        {
            _dirToIdlookup.TryGetValue(directory, out long id);
            return id;
        }

        private string GetDirectory(long id)
        {
            _idToDirLookup.TryGetValue(id, out string dir);
            return dir;
        }

        public bool HasMedia(long id) => _idToDirLookup.TryGetValue(id, out _);

        public IEnumerable<int> GetEpisodes(long id)
        {
            var dir = GetDirectory(id);

            if (string.IsNullOrEmpty(dir))
            {
                return Enumerable.Empty<int>();
            }

            var eps = new List<int>();
            foreach (var item in Directory.GetFileSystemEntries(dir))
            {
                var result = AnitomySharp.AnitomySharp.Parse(Path.GetFileName(item));

                if (result.FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementEpisodeNumber) is { } epResult)
                {
                    eps.Add(int.Parse(epResult.Value));
                }
            }

            eps.Sort();

            return eps;
        }

        public string GetMedia(long id, int ep)
        {
            var dir = GetDirectory(id);

            if (string.IsNullOrEmpty(dir))
            {
                return string.Empty;
            }

            foreach (var item in Directory.GetFileSystemEntries(dir))
            {
                var result = AnitomySharp.AnitomySharp.Parse(Path.GetFileName(item));

                if (result.FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementEpisodeNumber) is { } epResult && int.Parse(epResult.Value) == ep)
                {
                    return item;
                }
            }

            return string.Empty;
        }

        public string GetDirectory() => _localSettingsService.ReadSetting<string>("MediaFolder");
    }
}
