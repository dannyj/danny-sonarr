using System;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexWatchStatsSyncStateRepository : IBasicRepository<PlexWatchStatsSyncState>
    {
        PlexWatchStatsSyncState GetByServer(int plexServerDefinitionId);
        PlexWatchStatsSyncState MarkRunStarted(int plexServerDefinitionId, DateTime startedAtUtc);
        PlexWatchStatsSyncState MarkRunSucceeded(int plexServerDefinitionId, DateTime completedAtUtc, DateTime? lastSuccessfulViewedAtUtc, string lastSuccessfulEventKey, string message);
        PlexWatchStatsSyncState MarkRunFailed(int plexServerDefinitionId, DateTime completedAtUtc, string message);
    }

    public class PlexWatchStatsSyncStateRepository : BasicRepository<PlexWatchStatsSyncState>, IPlexWatchStatsSyncStateRepository
    {
        public PlexWatchStatsSyncStateRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public PlexWatchStatsSyncState GetByServer(int plexServerDefinitionId)
        {
            return Query(x => x.PlexServerDefinitionId == plexServerDefinitionId).SingleOrDefault();
        }

        public PlexWatchStatsSyncState MarkRunStarted(int plexServerDefinitionId, DateTime startedAtUtc)
        {
            var state = GetByServer(plexServerDefinitionId) ?? NewState(plexServerDefinitionId, startedAtUtc);

            state.LastRunStartedAtUtc = startedAtUtc;
            state.LastRunStatus = "Running";
            state.LastRunMessage = null;
            state.UpdatedAtUtc = startedAtUtc;

            return Upsert(state);
        }

        public PlexWatchStatsSyncState MarkRunSucceeded(int plexServerDefinitionId, DateTime completedAtUtc, DateTime? lastSuccessfulViewedAtUtc, string lastSuccessfulEventKey, string message)
        {
            var state = GetByServer(plexServerDefinitionId) ?? NewState(plexServerDefinitionId, completedAtUtc);

            state.LastRunCompletedAtUtc = completedAtUtc;
            state.LastRunStatus = "Success";
            state.LastRunMessage = message;
            state.LastSuccessfulViewedAtUtc = lastSuccessfulViewedAtUtc;
            state.LastSuccessfulEventKey = lastSuccessfulEventKey;
            state.FullResyncRequired = false;
            state.UpdatedAtUtc = completedAtUtc;

            return Upsert(state);
        }

        public PlexWatchStatsSyncState MarkRunFailed(int plexServerDefinitionId, DateTime completedAtUtc, string message)
        {
            var state = GetByServer(plexServerDefinitionId) ?? NewState(plexServerDefinitionId, completedAtUtc);

            state.LastRunCompletedAtUtc = completedAtUtc;
            state.LastRunStatus = "Failed";
            state.LastRunMessage = message;
            state.UpdatedAtUtc = completedAtUtc;

            return Upsert(state);
        }

        private static PlexWatchStatsSyncState NewState(int plexServerDefinitionId, DateTime now)
        {
            return new PlexWatchStatsSyncState
            {
                PlexServerDefinitionId = plexServerDefinitionId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }
    }
}
