using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public class PlexWatchStatsSyncState : ModelBase
    {
        public int PlexServerDefinitionId { get; set; }
        public DateTime? LastSuccessfulViewedAtUtc { get; set; }
        public string LastSuccessfulEventKey { get; set; }
        public DateTime? LastRunStartedAtUtc { get; set; }
        public DateTime? LastRunCompletedAtUtc { get; set; }
        public string LastRunStatus { get; set; }
        public string LastRunMessage { get; set; }
        public bool FullResyncRequired { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
