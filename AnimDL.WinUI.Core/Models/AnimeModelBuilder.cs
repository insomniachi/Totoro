using AnimDL.WinUI.Core.Contracts;
using MalApi;

namespace AnimDL.WinUI.Models;

public class MalToModelConverter
{
	private readonly ISchedule _schedule;

	public MalToModelConverter(ISchedule schedule)
	{
		_schedule = schedule;
	}

	public AnimeModel Convert<T>(Anime anime)
		where T : AnimeModel, new()
	{
		var model = new T();
		return model switch
		{
			SeasonalAnimeModel m => PopulateSeason(m, anime),
			ScheduledAnimeModel m => PopulateSchedule(m, anime),
			AnimeModel m => Populate(m, anime),
			_ => throw new Exception("Unsupported type argument")
		};
	}

	private AnimeModel Populate(AnimeModel model, Anime malModel)
	{
		model.Id = malModel.Id;
		model.Title = malModel.Title;
		model.Image = malModel.MainPicture.Large;
		model.UserAnimeStatus = malModel.UserStatus;
		model.TotalEpisodes = malModel.TotalEpisodes;
		return model;
	}

	private ScheduledAnimeModel PopulateSchedule(ScheduledAnimeModel model, Anime malModel)
	{
		Populate(model, malModel);

        var time = _schedule.GetTimeTillEpisodeAirs(model.Id);
        
		if (time is null)
        {
			model.BroadcastDay = malModel.Broadcast.DayOfWeek;
			return model;
        }

        model.BroadcastDay = (DateTime.Now + time.TimeSpan).DayOfWeek;
		model.TimeRemaining = time;
        return model;
	}

	private SeasonalAnimeModel PopulateSeason(SeasonalAnimeModel model, Anime malModel)
	{
		Populate(model, malModel);
		model.Season = malModel.StartSeason;

		if(model.Season == CurrentSeason())
		{
			PopulateSchedule(model, malModel);
		}

		return model;
	}

    private static Season CurrentSeason()
    {
        var date = DateTime.Now;
        var year = date.Year;
        var month = date.Month;

        var current = month switch
        {
            1 or 2 or 3 => AnimeSeason.Winter,
            4 or 5 or 6 => AnimeSeason.Spring,
            7 or 8 or 9 => AnimeSeason.Summer,
            10 or 11 or 12 => AnimeSeason.Fall,
            _ => throw new InvalidOperationException()
        };

        return new(current, year);
    }

}
