# Totoro

A Windows app for tracking and watching anime, built with WinUI 3 and .NET 10.

- **Your list, from AniList or MyAnimeList.** Browse and search anime, and track what you watch. When you're signed in to both, changes are made on both.
- **Watch episodes.** Episodes are found on an anime provider (SubsPlease) and streamed from torrents with rqbit. They play in the built-in player (libvlc), or in mpv.
- **Skips openings and endings.** Using Aniskip's times, it offers to skip them or skips them for you. It offers the next episode, and marks episodes watched when you finish them.
- **Episode numbering.** For providers that number episodes across seasons, it uses anime-relations, plus rules of your own.
- **Music.** Each anime's openings and endings, from AnimeThemes, to listen to or watch.
- **AI-agent friendly.** Everything the app does is also an agent tool (see [AGENTS.md](AGENTS.md)).

## Building

You need:

- Windows 10 (1809) or later
- The [.NET 10 SDK](https://dotnet.microsoft.com/download)
- The Windows App SDK's runtime, or Visual Studio 2026 with the *WinUI application development* workload

Then:

```bash
dotnet build src/Totoro/Totoro.csproj -c Release -p:Platform=x64
```

Or open `Totoro.slnx` in Visual Studio and run the `Totoro` project. The first build downloads rqbit, checking its SHA-256. libvlc comes from NuGet.

## Repository layout

```
├── src/
│   ├── Totoro/          The app: pages, view models, modules, playback, agent tools
│   ├── Totoro.Core/     What doesn't depend on WinUI: contracts, settings, services
│   └── Totoro.UI/       Reusable WinUI pieces: controls, dialogs, navigation, theming
├── Totoro.slnx          The solution
├── Directory.Build.props, Directory.Packages.props   Build settings and package versions
└── AGENTS.md            How the code is organised, and its conventions
```

## Thanks

- [rqbit](https://github.com/ikatson/rqbit) (Apache-2.0) streams torrents.
- [libvlc](https://www.videolan.org/vlc/libvlc.html) and [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) (LGPL-2.1) play video.
- [NAudio](https://github.com/naudio/NAudio) plays songs' audio.
- Anime data comes from [AniList](https://anilist.co), [MyAnimeList](https://myanimelist.net) and [Jikan](https://jikan.moe).
- Episode details and id mappings come from [ani.zip](https://ani.zip).
- Skip times come from [Aniskip](https://aniskip.com), episode numbering from [anime-relations](https://github.com/erengy/anime-relations), and songs from [AnimeThemes](https://animethemes.moe).
