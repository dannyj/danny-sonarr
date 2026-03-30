using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Notifications.Plex.Server;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public class RefreshPlexSeriesStatsService : IExecute<RefreshPlexSeriesStatsCommand>
    {
        private readonly INotificationFactory _notificationFactory;
        private readonly IPlexWatchStatsService _plexWatchStatsService;
        private readonly IPlexSeriesMatchService _plexSeriesMatchService;
        private readonly IPlexSeriesWatchStatisticsRepository _repository;
        private readonly IPlexProcessedWatchEventRepository _processedWatchEventRepository;
        private readonly IPlexWatchStatsSyncStateRepository _syncStateRepository;
        private readonly IPlexWatchTriggeredSearchService _plexWatchTriggeredSearchService;
        private readonly Logger _logger;
        private static readonly TimeSpan IncrementalOverlap = TimeSpan.FromHours(48);

        public RefreshPlexSeriesStatsService(INotificationFactory notificationFactory,
                                             IPlexWatchStatsService plexWatchStatsService,
                                             IPlexSeriesMatchService plexSeriesMatchService,
                                             IPlexSeriesWatchStatisticsRepository repository,
                                             IPlexProcessedWatchEventRepository processedWatchEventRepository,
                                             IPlexWatchStatsSyncStateRepository syncStateRepository,
                                             IPlexWatchTriggeredSearchService plexWatchTriggeredSearchService,
                                             Logger logger)
        {
            _notificationFactory = notificationFactory;
            _plexWatchStatsService = plexWatchStatsService;
            _plexSeriesMatchService = plexSeriesMatchService;
            _repository = repository;
            _processedWatchEventRepository = processedWatchEventRepository;
            _syncStateRepository = syncStateRepository;
            _plexWatchTriggeredSearchService = plexWatchTriggeredSearchService;
            _logger = logger;
        }

        public void Execute(RefreshPlexSeriesStatsCommand message)
        {
            var plexServers = _notificationFactory.GetAvailableProviders()
                .OfType<PlexServer>()
                .Where(x => GetSettings(x).ImportWatchStats && GetSettings(x).AuthToken.IsNotNullOrWhiteSpace())
                .ToList();

            var processedServers = 0;

            foreach (var plexServer in plexServers)
            {
                processedServers++;

                try
                {
                    ProcessServer(plexServer);
                }
                catch (PlexAuthenticationException ex)
                {
                    _syncStateRepository.MarkRunFailed(plexServer.Definition.Id, DateTime.UtcNow, ex.Message);
                    _logger.Warn(ex, "Skipping Plex watch stats sync for {0} due to authentication failure", plexServer.Definition.Name);
                }
                catch (PlexException ex)
                {
                    _syncStateRepository.MarkRunFailed(plexServer.Definition.Id, DateTime.UtcNow, ex.Message);
                    _logger.Warn(ex, "Skipping Plex watch stats sync for {0} due to connectivity failure", plexServer.Definition.Name);
                }
                catch (Exception ex)
                {
                    _syncStateRepository.MarkRunFailed(plexServer.Definition.Id, DateTime.UtcNow, ex.Message);
                    _logger.Warn(ex, "Skipping Plex watch stats sync for {0} due to unexpected error", plexServer.Definition.Name);
                }
            }

            _logger.Info("Finished Plex watch stats sync for {0} servers", processedServers);
        }

        private void ProcessServer(PlexServer plexServer)
        {
            var settings = GetSettings(plexServer);
            var runStartedAtUtc = DateTime.UtcNow;
            var syncState = _syncStateRepository.MarkRunStarted(plexServer.Definition.Id, runStartedAtUtc);
            var previousCheckpoint = syncState.LastSuccessfulViewedAtUtc;
            var fetchResult = _plexWatchStatsService.GetWatchEvents(settings, previousCheckpoint, IncrementalOverlap);
            var dedupeStart = DateTime.UtcNow;
            var existingKeys = _processedWatchEventRepository.GetExistingKeys(plexServer.Definition.Id, fetchResult.Events.Select(x => x.EventKey));
            var newEvents = fetchResult.Events
                .GroupBy(x => x.EventKey, StringComparer.Ordinal)
                .Select(x => x.OrderByDescending(y => y.ViewedAtUtc).First())
                .Where(x => !existingKeys.Contains(x.EventKey))
                .ToList();
            var dedupeDuration = DateTime.UtcNow - dedupeStart;
            var matchedEvents = new List<(Series Series, PlexWatchEvent WatchEvent)>();
            var skippedEvents = 0;
            var unmatchedEvents = 0;

            foreach (var watchEvent in newEvents)
            {
                try
                {
                    var match = _plexSeriesMatchService.Match(watchEvent);

                    if (match == null)
                    {
                        skippedEvents++;
                        unmatchedEvents++;
                        _logger.Debug("Skipping unmatched Plex watch event for {0}", watchEvent.SeriesTitle);
                        continue;
                    }

                    matchedEvents.Add((match, watchEvent));
                }
                catch (Exception ex)
                {
                    skippedEvents++;
                    _logger.Warn(ex, "Malformed Plex watch event encountered for server {0}", plexServer.Definition.Name);
                }
            }

            var now = DateTime.UtcNow;
            var matchedSeriesByEventKey = matchedEvents.ToDictionary(x => x.WatchEvent.EventKey, x => x.Series);
            var processedEvents = newEvents
                .Select(x =>
                {
                    matchedSeriesByEventKey.TryGetValue(x.EventKey, out var matchedSeries);

                    return new PlexProcessedWatchEvent
                    {
                        PlexServerDefinitionId = plexServer.Definition.Id,
                        EventKey = x.EventKey,
                        ViewedAtUtc = x.ViewedAtUtc,
                        ViewedOn = x.ViewedAtUtc.Date,
                        SeriesId = matchedSeries?.Id,
                        SeriesTitle = x.SeriesTitle,
                        FilePath = x.FilePath,
                        Matched = matchedSeries != null,
                        CreatedAtUtc = now
                    };
                })
                .ToList();

            var aggregated = matchedEvents
                .GroupBy(x => new
                {
                    x.Series.Id,
                    ViewedOn = x.WatchEvent.ViewedAtUtc.Date
                })
                .Select(x => new PlexSeriesWatchStatisticDelta
                {
                    SeriesId = x.Key.Id,
                    PlexServerDefinitionId = plexServer.Definition.Id,
                    ViewedOn = x.Key.ViewedOn,
                    ViewCount = x.Count(),
                    LastViewedAtUtc = x.Max(v => v.WatchEvent.ViewedAtUtc),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                })
                .ToList();

            _processedWatchEventRepository.InsertMany(processedEvents);
            _repository.UpsertDeltas(aggregated);
            _plexWatchTriggeredSearchService.Process(plexServer.Definition.Id, settings, matchedEvents, previousCheckpoint.HasValue);
            var completedAtUtc = DateTime.UtcNow;
            var newestProcessedEvent = processedEvents
                .OrderByDescending(x => x.ViewedAtUtc)
                .ThenByDescending(x => x.EventKey, StringComparer.Ordinal)
                .FirstOrDefault();

            _syncStateRepository.MarkRunSucceeded(
                plexServer.Definition.Id,
                completedAtUtc,
                newestProcessedEvent?.ViewedAtUtc ?? syncState.LastSuccessfulViewedAtUtc,
                newestProcessedEvent?.EventKey ?? syncState.LastSuccessfulEventKey,
                $"Fetched {fetchResult.RawEventsFetched} events across {fetchResult.PagesFetched} pages; accepted {newEvents.Count} new events");

            _logger.Info("Plex watch stats sync complete for {0}: fetched {1} events across {2} pages, older skipped {3}, duplicates ignored {4}, accepted {5}, matched {6}, skipped {7}, unmatched {8}, series updated {9}, dedupe {10}ms",
                plexServer.Definition.Name,
                fetchResult.RawEventsFetched,
                fetchResult.PagesFetched,
                fetchResult.OlderEventsSkipped,
                existingKeys.Count,
                newEvents.Count,
                matchedEvents.Count,
                skippedEvents,
                unmatchedEvents,
                aggregated.Select(x => x.SeriesId).Distinct().Count(),
                (int)dedupeDuration.TotalMilliseconds);
        }

        private static PlexServerSettings GetSettings(PlexServer plexServer)
        {
            return (PlexServerSettings)plexServer.Definition.Settings;
        }
    }
}
