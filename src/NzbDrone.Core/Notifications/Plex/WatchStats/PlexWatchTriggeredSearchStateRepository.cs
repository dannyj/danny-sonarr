using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexWatchTriggeredSearchStateRepository : IBasicRepository<PlexWatchTriggeredSearchState>
    {
        Dictionary<int, PlexWatchTriggeredSearchState> GetByServer(int plexServerDefinitionId, IEnumerable<int> seriesIds);
    }

    public class PlexWatchTriggeredSearchStateRepository : BasicRepository<PlexWatchTriggeredSearchState>, IPlexWatchTriggeredSearchStateRepository
    {
        public PlexWatchTriggeredSearchStateRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Dictionary<int, PlexWatchTriggeredSearchState> GetByServer(int plexServerDefinitionId, IEnumerable<int> seriesIds)
        {
            var ids = seriesIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<int, PlexWatchTriggeredSearchState>();
            }

            return Query(x => x.PlexServerDefinitionId == plexServerDefinitionId && ids.Contains(x.SeriesId))
                .ToDictionary(x => x.SeriesId, x => x);
        }
    }
}
