using System;
using System.Globalization;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public class PlexSeriesWatchStatistic : ModelBase
    {
        public int SeriesId { get; set; }
        public int PlexServerDefinitionId { get; set; }
        public DateTime ViewedOn { get; set; }
        public int ViewCount { get; set; }
        public DateTime? LastViewedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class PlexSeriesWatchStatisticsAggregate
    {
        public int SeriesId { get; set; }
        public int ViewsLast30Days { get; set; }
        public int ViewsPrevious30Days { get; set; }
        public int ViewsAllTime { get; set; }
        public string LastViewedAtString { get; set; }

        public DateTime? LastViewedAt
        {
            get
            {
                if (LastViewedAtString == null)
                {
                    return null;
                }

                if (DateTime.TryParse(LastViewedAtString, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    return parsed;
                }

                return null;
            }
        }
    }
}
