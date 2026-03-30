using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public class PlexProcessedWatchEvent : ModelBase
    {
        public int PlexServerDefinitionId { get; set; }
        public string EventKey { get; set; }
        public DateTime ViewedAtUtc { get; set; }
        public DateTime ViewedOn { get; set; }
        public int? SeriesId { get; set; }
        public string SeriesTitle { get; set; }
        public string FilePath { get; set; }
        public bool Matched { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
