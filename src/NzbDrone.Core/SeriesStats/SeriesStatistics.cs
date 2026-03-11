using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Notifications.Plex.WatchStats;

namespace NzbDrone.Core.SeriesStats
{
    public class SeriesStatistics : ResultSet
    {
        public int SeriesId { get; set; }
        public DateTime? NextAiring { get; set; }
        public DateTime? PreviousAiring { get; set; }
        public DateTime? LastAired { get; set; }
        public int EpisodeFileCount { get; set; }
        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public int MonitoredEpisodeCount { get; set; }
        public long SizeOnDisk { get; set; }
        public bool WatchedOnPlex { get; set; }
        public bool NeverWatchedOnPlex { get; set; }
        public int ViewsLast30Days { get; set; }
        public int ViewsPrevious30Days { get; set; }
        public int ViewsAllTime { get; set; }
        public DateTime? LastViewedAt { get; set; }
        public int? DaysSinceLastView { get; set; }
        public PlexViewTrend ViewTrend { get; set; }
        public List<string> ReleaseGroups { get; set; }
        public List<SeasonStatistics> SeasonStatistics { get; set; }
    }
}
