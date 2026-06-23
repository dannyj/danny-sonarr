using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Notifications.Plex.WatchStats;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.SeriesStats
{
    public interface ISeriesStatisticsService
    {
        List<SeriesStatistics> SeriesStatistics();
        SeriesStatistics SeriesStatistics(int seriesId, int qualityProfileId);
    }

    public class SeriesStatisticsService : ISeriesStatisticsService
    {
        private readonly ISeriesStatisticsRepository _seriesStatisticsRepository;
        private readonly IPlexSeriesWatchStatisticsRepository _plexSeriesWatchStatisticsRepository;
        private readonly ISeriesService _seriesService;
        private readonly IQualityProfileService _qualityProfileService;

        public SeriesStatisticsService(ISeriesStatisticsRepository seriesStatisticsRepository,
                                       IPlexSeriesWatchStatisticsRepository plexSeriesWatchStatisticsRepository,
                                       ISeriesService seriesService,
                                       IQualityProfileService qualityProfileService)
        {
            _seriesStatisticsRepository = seriesStatisticsRepository;
            _plexSeriesWatchStatisticsRepository = plexSeriesWatchStatisticsRepository;
            _seriesService = seriesService;
            _qualityProfileService = qualityProfileService;
        }

        public List<SeriesStatistics> SeriesStatistics()
        {
            var seasonStatistics = _seriesStatisticsRepository.SeriesStatistics();
            var plexStatistics = _plexSeriesWatchStatisticsRepository.GetAggregates();
            var seriesProfiles = _seriesService.GetAllSeriesQualityProfiles();
            var profiles = _qualityProfileService.All().ToDictionary(p => p.Id);

            var result = seasonStatistics
                .GroupBy(s => s.SeriesId)
                .Select(s =>
                {
                    var profileId = seriesProfiles.GetValueOrDefault(s.Key);
                    profiles.TryGetValue(profileId, out var profile);
                    return MapSeriesStatistics(s.ToList(), profile, plexStatistics.GetValueOrDefault(s.Key));
                })
                .ToList();

            foreach (var plexOnlyStatistic in plexStatistics.Values.Where(x => result.All(r => r.SeriesId != x.SeriesId)))
            {
                result.Add(MergePlexStatistics(new SeriesStatistics(), plexOnlyStatistic));
            }

            return result;
        }

        public SeriesStatistics SeriesStatistics(int seriesId, int qualityProfileId)
        {
            var stats = _seriesStatisticsRepository.SeriesStatistics(seriesId);

            if (stats == null || stats.Count == 0)
            {
                return MergePlexStatistics(new SeriesStatistics(), _plexSeriesWatchStatisticsRepository.GetAggregate(seriesId));
            }

            var profile = _qualityProfileService.Get(qualityProfileId);

            return MapSeriesStatistics(stats, profile, _plexSeriesWatchStatisticsRepository.GetAggregate(seriesId));
        }

        private SeriesStatistics MapSeriesStatistics(List<SeasonStatistics> seasonStatistics, QualityProfile profile, PlexSeriesWatchStatisticsAggregate plexStatistics)
        {
            var seriesStatistics = new SeriesStatistics
            {
                SeasonStatistics = seasonStatistics,
                SeriesId = seasonStatistics.First().SeriesId,
                EpisodeFileCount = seasonStatistics.Sum(s => s.EpisodeFileCount),
                EpisodeCount = seasonStatistics.Sum(s => s.EpisodeCount),
                TotalEpisodeCount = seasonStatistics.Sum(s => s.TotalEpisodeCount),
                MonitoredEpisodeCount = seasonStatistics.Sum(s => s.MonitoredEpisodeCount),
                SizeOnDisk = seasonStatistics.Sum(s => s.SizeOnDisk),
                ReleaseGroups = seasonStatistics.SelectMany(s => s.ReleaseGroups).Distinct().ToList(),
                ReleaseTypes = seasonStatistics.SelectMany(s => s.ReleaseTypes).Distinct().OrderBy(s => s).ToList(),
                EpisodeFileQualities = SortQualities(seasonStatistics.SelectMany(s => s.EpisodeFileQualities).Distinct().ToList(), profile)
            };

            var nextAiring = seasonStatistics.Where(s => s.NextAiring != null).MinBy(s => s.NextAiring);
            var previousAiring = seasonStatistics.Where(s => s.PreviousAiring != null).MaxBy(s => s.PreviousAiring);
            var lastAired = seasonStatistics.Where(s => s.SeasonNumber > 0 && s.LastAired != null).MaxBy(s => s.LastAired);

            seriesStatistics.NextAiring = nextAiring?.NextAiring;
            seriesStatistics.PreviousAiring = previousAiring?.PreviousAiring;
            seriesStatistics.LastAired = lastAired?.LastAired;

            return MergePlexStatistics(seriesStatistics, plexStatistics);
        }

        private SeriesStatistics MergePlexStatistics(SeriesStatistics seriesStatistics, PlexSeriesWatchStatisticsAggregate plexStatistics)
        {
            plexStatistics ??= new PlexSeriesWatchStatisticsAggregate
            {
                SeriesId = seriesStatistics.SeriesId
            };

            if (seriesStatistics.SeriesId == 0)
            {
                seriesStatistics.SeriesId = plexStatistics.SeriesId;
            }

            seriesStatistics.ViewsLast30Days = plexStatistics.ViewsLast30Days;
            seriesStatistics.ViewsPrevious30Days = plexStatistics.ViewsPrevious30Days;
            seriesStatistics.ViewsAllTime = plexStatistics.ViewsAllTime;
            seriesStatistics.LastViewedAt = plexStatistics.LastViewedAt;
            seriesStatistics.WatchedOnPlex = plexStatistics.ViewsAllTime > 0;
            seriesStatistics.NeverWatchedOnPlex = plexStatistics.ViewsAllTime == 0;

            if (plexStatistics.LastViewedAt.HasValue)
            {
                seriesStatistics.DaysSinceLastView = (System.DateTime.UtcNow.Date - plexStatistics.LastViewedAt.Value.Date).Days;
            }

            if (plexStatistics.ViewsLast30Days == 0 && plexStatistics.ViewsPrevious30Days == 0)
            {
                seriesStatistics.ViewTrend = PlexViewTrend.None;
            }
            else if (plexStatistics.ViewsLast30Days > plexStatistics.ViewsPrevious30Days)
            {
                seriesStatistics.ViewTrend = PlexViewTrend.Up;
            }
            else if (plexStatistics.ViewsLast30Days < plexStatistics.ViewsPrevious30Days)
            {
                seriesStatistics.ViewTrend = PlexViewTrend.Down;
            }
            else
            {
                seriesStatistics.ViewTrend = PlexViewTrend.Flat;
            }

            return seriesStatistics;
        }

        private static List<Quality> SortQualities(List<Quality> qualities, QualityProfile profile)
        {
            if (profile == null)
            {
                return qualities;
            }

            return qualities.OrderBy(q => profile.GetIndex(q.Id).Index).ToList();
        }
    }
}
