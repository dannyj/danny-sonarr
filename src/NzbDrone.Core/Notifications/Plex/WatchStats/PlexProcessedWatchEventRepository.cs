using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexProcessedWatchEventRepository : IBasicRepository<PlexProcessedWatchEvent>
    {
        HashSet<string> GetExistingKeys(int plexServerDefinitionId, IEnumerable<string> eventKeys);
    }

    public class PlexProcessedWatchEventRepository : BasicRepository<PlexProcessedWatchEvent>, IPlexProcessedWatchEventRepository
    {
        public PlexProcessedWatchEventRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public HashSet<string> GetExistingKeys(int plexServerDefinitionId, IEnumerable<string> eventKeys)
        {
            var keys = eventKeys.Distinct().ToList();

            if (keys.Count == 0)
            {
                return new HashSet<string>();
            }

            const string sql = @"SELECT ""EventKey""
                                 FROM ""PlexProcessedWatchEvents""
                                 WHERE ""PlexServerDefinitionId"" = @plexServerDefinitionId
                                   AND ""EventKey"" IN @keys";

            using (var conn = _database.OpenConnection())
            {
                return conn.Query<string>(sql, new { plexServerDefinitionId, keys }).ToHashSet();
            }
        }
    }
}
