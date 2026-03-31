using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Dashboard
{
    public class DashboardSnapshot : ModelBase
    {
        public DateTime SnapshotDate { get; set; }
        public int SeriesCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public int EpisodeFileCount { get; set; }
        public long TotalSizeOnDisk { get; set; }
        public int ViewsAllTime { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
