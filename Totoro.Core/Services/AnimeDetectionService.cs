using System.Linq;
using AnitomySharp;
using FuzzySharp;
using Humanizer;
using Splat;
using Totoro.Core.Contracts;

namespace Totoro.Core.Services;

internal class AnimeDetectionService : IAnimeDetectionService, IEnableLogger
{
    private readonly IViewService _viewService;
    private readonly IToastService _toastService;
    private readonly IAnimeServiceContext _animeServiceContext;

    public AnimeDetectionService(IViewService viewService,
                                 IToastService toastService,
                                 IAnimeServiceContext animeServiceContext)
    {
        _viewService = viewService;
        _toastService = toastService;
        _animeServiceContext = animeServiceContext;
    }

    public Task<long?> DetectFromFileName(string fileName, bool useNotification = false)
    {
        var anitomyResult = AnitomySharp.AnitomySharp.Parse(fileName);

        if(anitomyResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementAnimeTitle) is not { } titleResult)
        {
            return null;
        }

        var title = titleResult.Value;

        if(anitomyResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementAnimeSeason) is { } seasonResult)
        {
            var season = int.Parse(seasonResult.Value);
            title += $" {season.Ordinalize()} Season";
        }

        return DetectFromTitle(title, useNotification);
    }

    public async Task<long?> DetectFromTitle(string title, bool useNotification = false)
    {
        var candidates = Enumerable.Empty<AnimeModel>();
        try
        {
            var trimmed = title[..Math.Min(title.Length, 50)].Trim();
            candidates = await _animeServiceContext.GetAnime(trimmed);
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return null;
        }

        if (!candidates.Any())
        {
            this.Log().Fatal($"no candidates found for title {title}");
            return null;
        }

        if (candidates.FirstOrDefault(x => x.Title == title || x.AlternativeTitles.Any(x => x == title)) is { } result)
        {
            return result.Id;
        }

        var ratios = candidates.Select(x => GetRatio(x, title)).OrderByDescending(x => x.Item2).ToList();
        var filtered = ratios.Where(x => x.Item2 > 70).ToList();
        if (filtered.Count == 1)
        {
            return filtered[0].Item1.Id;
        }
        else
        {
            var hundredPercent = filtered.Where(x => x.Item2 == 100).ToList();
            if (hundredPercent.Count == 1)
            {
                return hundredPercent[0].Item1.Id;
            }

            if(useNotification)
            {
                _toastService.PromptAnimeSelection(candidates, filtered.FirstOrDefault().Item1 ?? candidates.FirstOrDefault());
                return null;
            }
            else
            {
                var model = await _viewService.SelectModel(candidates, filtered.FirstOrDefault().Item1 ?? candidates.FirstOrDefault(), _animeServiceContext.GetAnime);
                return model?.Id;
            }
        }
    }


    private static (AnimeModel, double) GetRatio(AnimeModel anime, string title)
    {
        var titleRatio = Fuzz.Ratio(anime.Title, title);
        var maxAltTitleRatio = anime.AlternativeTitles.Select(x => Fuzz.Ratio(x, title)).Max();
        return (anime, Math.Max(titleRatio, maxAltTitleRatio));
    }
}
