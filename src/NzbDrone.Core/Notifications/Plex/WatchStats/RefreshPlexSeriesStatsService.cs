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
        private readonly IPlexWatchTriggeredSearchService _plexWatchTriggeredSearchService;
        private readonly Logger _logger;

        public RefreshPlexSeriesStatsService(INotificationFactory notificationFactory,
                                             IPlexWatchStatsService plexWatchStatsService,
                                             IPlexSeriesMatchService plexSeriesMatchService,
                                             IPlexSeriesWatchStatisticsRepository repository,
                                             IPlexWatchTriggeredSearchService plexWatchTriggeredSearchService,
                                             Logger logger)
        {
            _notificationFactory = notificationFactory;
            _plexWatchStatsService = plexWatchStatsService;
            _plexSeriesMatchService = plexSeriesMatchService;
            _repository = repository;
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
                    _logger.Warn(ex, "Skipping Plex watch stats sync for {0} due to authentication failure", plexServer.Definition.Name);
                }
                catch (PlexException ex)
                {
                    _logger.Warn(ex, "Skipping Plex watch stats sync for {0} due to connectivity failure", plexServer.Definition.Name);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Skipping Plex watch stats sync for {0} due to unexpected error", plexServer.Definition.Name);
                }
            }

            _logger.Info("Finished Plex watch stats sync for {0} servers", processedServers);
        }

        private void ProcessServer(PlexServer plexServer)
        {
            var settings = GetSettings(plexServer);
            var watchEvents = _plexWatchStatsService.GetWatchEvents(settings);
            var matchedEvents = new List<(Series Series, PlexWatchEvent WatchEvent)>();
            var skippedEvents = 0;

            foreach (var watchEvent in watchEvents)
            {
                try
                {
                    var match = _plexSeriesMatchService.Match(watchEvent);

                    if (match == null)
                    {
                        skippedEvents++;
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
            var aggregated = matchedEvents
                .GroupBy(x => new
                {
                    x.Series.Id,
                    ViewedOn = x.WatchEvent.ViewedAtUtc.Date
                })
                .Select(x => new PlexSeriesWatchStatistic
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

            _repository.ReplaceForServer(plexServer.Definition.Id, aggregated);
            _plexWatchTriggeredSearchService.Process(plexServer.Definition.Id, settings, matchedEvents);

            _logger.Info("Plex watch stats sync complete for {0}: fetched {1} events, matched {2}, skipped {3}, series updated {4}",
                plexServer.Definition.Name,
                watchEvents.Count,
                matchedEvents.Count,
                skippedEvents,
                aggregated.Select(x => x.SeriesId).Distinct().Count());
        }

        private static PlexServerSettings GetSettings(PlexServer plexServer)
        {
            return (PlexServerSettings)plexServer.Definition.Settings;
        }
    }
}
