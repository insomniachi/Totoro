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
        
		if (time == TimeSpan.Zero)
        {
			model.BroadcastDay = malModel.Broadcast.DayOfWeek;
			return model;
        }

        model.BroadcastDay = (DateTime.Now + time).DayOfWeek;
		model.TimeToAir = time;
        return model;
	}

	private SeasonalAnimeModel PopulateSeason(SeasonalAnimeModel model, Anime malModel)
	{
		Populate(model, malModel);
		model.Season = malModel.StartSeason;
		return model;
	}

}
