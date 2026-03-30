using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public class PlexWatchTriggeredSearchState : ModelBase
    {
        public int SeriesId { get; set; }
        public int PlexServerDefinitionId { get; set; }
        public DateTime? LastSeenViewedAtUtc { get; set; }
        public DateTime? LastTriggeredAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
