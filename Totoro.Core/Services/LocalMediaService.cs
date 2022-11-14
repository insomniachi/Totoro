namespace Totoro.Core.Services
{
    public class LocalMediaService : ILocalMediaService
    {
        private readonly ILocalSettingsService _localSettingsService;
        private readonly Dictionary<string, long> _lookup;

        public LocalMediaService(ILocalSettingsService localSettingsService)
        {
            _localSettingsService = localSettingsService;
            _lookup = _localSettingsService.ReadSetting<Dictionary<string, long>>("LocalMedia", new());
        }

        public void SetId(string directory, long id)
        {
            _lookup[directory] = id;
            _localSettingsService.SaveSetting("LocalMedia", _lookup);
        }

        public long GetId(string directory)
        {
            _lookup.TryGetValue(directory, out long id);
            return id;
        }

        public string GetDirectory() => _localSettingsService.ReadSetting<string>("MediaFolder");
    }
}
