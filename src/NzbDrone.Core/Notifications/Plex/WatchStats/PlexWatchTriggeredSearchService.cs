using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Notifications.Plex.Server;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexWatchTriggeredSearchService
    {
        void Process(int plexServerDefinitionId, PlexServerSettings settings, List<(Series Series, PlexWatchEvent WatchEvent)> matchedEvents);
    }

    public class PlexWatchTriggeredSearchService : IPlexWatchTriggeredSearchService
    {
        private readonly IPlexWatchTriggeredSearchStateRepository _stateRepository;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public PlexWatchTriggeredSearchService(IPlexWatchTriggeredSearchStateRepository stateRepository,
                                               IManageCommandQueue commandQueueManager,
                                               Logger logger)
        {
            _stateRepository = stateRepository;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public void Process(int plexServerDefinitionId, PlexServerSettings settings, List<(Series Series, PlexWatchEvent WatchEvent)> matchedEvents)
        {
            if (!settings.TriggerMissingEpisodeSearchOnWatch || matchedEvents.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var cooldown = TimeSpan.FromHours(settings.WatchTriggeredSearchCooldownHours);
            var groupedEvents = matchedEvents
                .GroupBy(x => x.Series.Id)
                .Select(x => new
                {
                    Series = x.First().Series,
                    LastViewedAtUtc = x.Max(v => v.WatchEvent.ViewedAtUtc)
                })
                .ToList();

            var states = _stateRepository.GetByServer(plexServerDefinitionId, groupedEvents.Select(x => x.Series.Id));
            var changedStates = new List<PlexWatchTriggeredSearchState>();
            var triggeredSeries = 0;

            foreach (var group in groupedEvents)
            {
                if (!states.TryGetValue(group.Series.Id, out var state))
                {
                    state = new PlexWatchTriggeredSearchState
                    {
                        SeriesId = group.Series.Id,
                        PlexServerDefinitionId = plexServerDefinitionId,
                        CreatedAtUtc = now
                    };
                }

                var hasNewPlayback = !state.LastSeenViewedAtUtc.HasValue || group.LastViewedAtUtc > state.LastSeenViewedAtUtc.Value;

                if (!hasNewPlayback)
                {
                    continue;
                }

                state.LastSeenViewedAtUtc = group.LastViewedAtUtc;
                state.UpdatedAtUtc = now;

                if (!group.Series.Monitored)
                {
                    _logger.Debug("Skipping Plex watch-triggered search for {0} because the series is not monitored", group.Series.Title);
                    changedStates.Add(state);
                    continue;
                }

                if (state.LastTriggeredAtUtc.HasValue && now - state.LastTriggeredAtUtc.Value < cooldown)
                {
                    _logger.Debug("Skipping Plex watch-triggered search for {0} because the cooldown is still active", group.Series.Title);
                    changedStates.Add(state);
                    continue;
                }

                _commandQueueManager.Push(new RefreshSeriesCommand(new List<int> { group.Series.Id }));
                _commandQueueManager.Push(new RescanSeriesCommand(group.Series.Id));
                _commandQueueManager.Push(new MissingEpisodeSearchCommand(group.Series.Id));

                state.LastTriggeredAtUtc = now;
                changedStates.Add(state);
                triggeredSeries++;

                _logger.Info("Queued watch-triggered refresh, rescan, and missing episode search for {0}", group.Series.Title);
            }

            foreach (var state in changedStates)
            {
                _stateRepository.Upsert(state);
            }

            if (triggeredSeries > 0)
            {
                _logger.Info("Queued Plex watch-triggered missing episode searches for {0} series", triggeredSeries);
            }
        }
    }
}
