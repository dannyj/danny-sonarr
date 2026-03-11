using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public class PlexMetadataGuid
    {
        public string Source { get; set; }
        public string Value { get; set; }
    }

    public class PlexUserRef
    {
        public string Id { get; set; }
    }

    public class PlexWatchEvent
    {
        public DateTime ViewedAtUtc { get; set; }
        public string SeriesTitle { get; set; }
        public int? SeriesYear { get; set; }
        public string FilePath { get; set; }
        public PlexUserRef User { get; set; }
        public List<PlexMetadataGuid> Guids { get; set; } = new();
    }
}
