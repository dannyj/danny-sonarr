using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Notifications.Plex.WatchStats;
using NzbDrone.Core.SeriesStats;

namespace Sonarr.Api.V3.Series
{
    public class SeriesStatisticsResource
    {
        public int SeasonCount { get; set; }
        public int EpisodeFileCount { get; set; }
        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public int MonitoredEpisodeCount { get; set; }
        public long SizeOnDisk { get; set; }
        public List<string> ReleaseGroups { get; set; }
        public bool WatchedOnPlex { get; set; }
        public bool NeverWatchedOnPlex { get; set; }
        public int ViewsLast30Days { get; set; }
        public int ViewsPrevious30Days { get; set; }
        public int ViewsAllTime { get; set; }
        public DateTime? LastViewedAt { get; set; }
        public int? DaysSinceLastView { get; set; }
        public PlexViewTrend ViewTrend { get; set; }

        public decimal PercentOfEpisodes
        {
            get
            {
                if (EpisodeCount == 0)
                {
                    return 0;
                }

                return (decimal)EpisodeFileCount / (decimal)EpisodeCount * 100;
            }
        }
    }

    public static class SeriesStatisticsResourceMapper
    {
        public static SeriesStatisticsResource ToResource(this SeriesStatistics model, List<SeasonResource> seasons)
        {
            if (model == null)
            {
                return null;
            }

            return new SeriesStatisticsResource
            {
                SeasonCount = seasons == null ? 0 : seasons.Where(s => s.SeasonNumber > 0).Count(),
                EpisodeFileCount = model.EpisodeFileCount,
                EpisodeCount = model.EpisodeCount,
                TotalEpisodeCount = model.TotalEpisodeCount,
                MonitoredEpisodeCount = model.MonitoredEpisodeCount,
                SizeOnDisk = model.SizeOnDisk,
                ReleaseGroups = model.ReleaseGroups,
                WatchedOnPlex = model.WatchedOnPlex,
                NeverWatchedOnPlex = model.NeverWatchedOnPlex,
                ViewsLast30Days = model.ViewsLast30Days,
                ViewsPrevious30Days = model.ViewsPrevious30Days,
                ViewsAllTime = model.ViewsAllTime,
                LastViewedAt = model.LastViewedAt,
                DaysSinceLastView = model.DaysSinceLastView,
                ViewTrend = model.ViewTrend
            };
        }
    }
}
