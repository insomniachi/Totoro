using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.WinUI.Dialogs.ViewModels
{
    public class EditAnimePreferencesViewModel : DialogViewModel
    {
        private readonly IAnimePreferencesService _animePreferencesService;
        private AnimePreferences _originalPreferences = null;
        private long _id;

        public EditAnimePreferencesViewModel(IAnimePreferencesService animePreferencesService)
        {
            _animePreferencesService = animePreferencesService;

            SavePreferencesCommand = ReactiveCommand.Create(Save);
            ResetProviderCommand = ReactiveCommand.Create(ResetProvider);
        }

        public void Initialize(long id)
        {
            _id = id;
            if(_animePreferencesService.HasPreferences(id))
            {
                _originalPreferences = _animePreferencesService.GetPreferences(id);
                Preferences = Clone(_originalPreferences);
            }
            else
            {
                Preferences = new();
            }

        }

        [Reactive] public AnimePreferences Preferences { get; set; }
        public ICommand SavePreferencesCommand { get; }
        public ICommand ResetProviderCommand { get; }

        public IEnumerable<string> Providers => ["", ..PluginFactory<AnimeProvider>.Instance.Plugins.Select(p => p.Name)];

        private void Save()
        {
            if(_originalPreferences is null)
            {
                _animePreferencesService.AddPreferences(_id, Preferences);
            }
            else
            {
                _originalPreferences.Alias = Preferences.Alias;
                _originalPreferences.StreamType = Preferences.StreamType;
                _originalPreferences.Provider = Preferences.Provider;
                _originalPreferences.PreferDubs = Preferences.PreferDubs;
            }

            _animePreferencesService.Save();
        }

        private void ResetProvider()
        {
            Preferences.Provider = string.Empty;
        }

        private static AnimePreferences Clone(AnimePreferences animePreferences) 
        {
            return new AnimePreferences
            {
                Alias = animePreferences.Alias,
                StreamType = animePreferences.StreamType,
                Provider = animePreferences.Provider,
                PreferDubs = animePreferences.PreferDubs
            };
        }
    }
}
