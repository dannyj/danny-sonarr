using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Plex.Server;
using NzbDrone.Core.Notifications.Plex.WatchStats;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.NotificationTests.Plex
{
    [TestFixture]
    public class RefreshPlexSeriesStatsServiceFixture : CoreTest<RefreshPlexSeriesStatsService>
    {
        private PlexServer _plexServer;
        private PlexServerSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new PlexServerSettings
            {
                Host = "localhost",
                AuthToken = "token",
                ImportWatchStats = true,
                TriggerMissingEpisodeSearchOnWatch = true,
                WatchTriggeredSearchCooldownHours = 1
            };

            _plexServer = Mocker.Resolve<PlexServer>();
            _plexServer.Definition = new NotificationDefinition
            {
                Id = 5,
                Name = "Plex",
                Settings = _settings
            };

            Mocker.GetMock<INotificationFactory>()
                .Setup(x => x.GetAvailableProviders())
                .Returns(new List<INotification> { _plexServer });
        }

        [Test]
        public void should_only_process_new_events_and_update_checkpoint()
        {
            var existingSeries = new Series { Id = 10, Title = "30 Rock", Monitored = true };
            var oldEvent = new PlexWatchEvent
            {
                EventKey = "old",
                ViewedAtUtc = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
                SeriesTitle = "30 Rock"
            };
            var newEvent = new PlexWatchEvent
            {
                EventKey = "new",
                ViewedAtUtc = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc),
                SeriesTitle = "30 Rock"
            };

            Mocker.GetMock<IPlexWatchStatsSyncStateRepository>()
                .Setup(x => x.MarkRunStarted(5, It.IsAny<DateTime>()))
                .Returns(new PlexWatchStatsSyncState
                {
                    PlexServerDefinitionId = 5,
                    LastSuccessfulViewedAtUtc = oldEvent.ViewedAtUtc,
                    LastSuccessfulEventKey = "old"
                });

            Mocker.GetMock<IPlexWatchStatsService>()
                .Setup(x => x.GetWatchEvents(_settings, oldEvent.ViewedAtUtc, It.IsAny<TimeSpan>()))
                .Returns(new PlexWatchEventFetchResult
                {
                    Events = new List<PlexWatchEvent> { oldEvent, newEvent },
                    RawEventsFetched = 2,
                    PagesFetched = 1
                });

            Mocker.GetMock<IPlexProcessedWatchEventRepository>()
                .Setup(x => x.GetExistingKeys(5, It.IsAny<IEnumerable<string>>()))
                .Returns(new HashSet<string> { "old" });

            Mocker.GetMock<IPlexSeriesMatchService>()
                .Setup(x => x.Match(It.Is<PlexWatchEvent>(e => e.EventKey == "new")))
                .Returns(existingSeries);

            Subject.Execute(new RefreshPlexSeriesStatsCommand());

            Mocker.GetMock<IPlexProcessedWatchEventRepository>()
                .Verify(x => x.InsertMany(It.Is<IList<PlexProcessedWatchEvent>>(events => events.Count == 1 && events[0].EventKey == "new" && events[0].Matched)), Times.Once);
            Mocker.GetMock<IPlexSeriesWatchStatisticsRepository>()
                .Verify(x => x.UpsertDeltas(It.Is<IList<PlexSeriesWatchStatisticDelta>>(deltas => deltas.Count == 1 && deltas[0].SeriesId == existingSeries.Id && deltas[0].ViewCount == 1)), Times.Once);
            Mocker.GetMock<IPlexWatchTriggeredSearchService>()
                .Verify(x => x.Process(5, _settings, It.Is<List<(Series Series, PlexWatchEvent WatchEvent)>>(events => events.Count == 1 && events[0].WatchEvent.EventKey == "new"), true), Times.Once);
            Mocker.GetMock<IPlexWatchStatsSyncStateRepository>()
                .Verify(x => x.MarkRunSucceeded(5, It.IsAny<DateTime>(), newEvent.ViewedAtUtc, "new", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void should_seed_trigger_state_without_queueing_searches_on_initial_import()
        {
            var series = new Series { Id = 10, Title = "30 Rock", Monitored = true };
            var newEvent = new PlexWatchEvent
            {
                EventKey = "new",
                ViewedAtUtc = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc),
                SeriesTitle = "30 Rock"
            };

            Mocker.GetMock<IPlexWatchStatsSyncStateRepository>()
                .Setup(x => x.MarkRunStarted(5, It.IsAny<DateTime>()))
                .Returns(new PlexWatchStatsSyncState
                {
                    PlexServerDefinitionId = 5
                });

            Mocker.GetMock<IPlexWatchStatsService>()
                .Setup(x => x.GetWatchEvents(_settings, null, It.IsAny<TimeSpan>()))
                .Returns(new PlexWatchEventFetchResult
                {
                    Events = new List<PlexWatchEvent> { newEvent },
                    RawEventsFetched = 1,
                    PagesFetched = 1
                });

            Mocker.GetMock<IPlexProcessedWatchEventRepository>()
                .Setup(x => x.GetExistingKeys(5, It.IsAny<IEnumerable<string>>()))
                .Returns(new HashSet<string>());

            Mocker.GetMock<IPlexSeriesMatchService>()
                .Setup(x => x.Match(newEvent))
                .Returns(series);

            Subject.Execute(new RefreshPlexSeriesStatsCommand());

            Mocker.GetMock<IPlexWatchTriggeredSearchService>()
                .Verify(x => x.Process(5, _settings, It.IsAny<List<(Series Series, PlexWatchEvent WatchEvent)>>(), false), Times.Once);
        }
    }
}
