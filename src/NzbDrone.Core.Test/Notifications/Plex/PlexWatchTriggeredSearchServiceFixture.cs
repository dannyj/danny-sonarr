using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Notifications.Plex.Server;
using NzbDrone.Core.Notifications.Plex.WatchStats;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Test.NotificationTests.Plex
{
    [TestFixture]
    public class PlexWatchTriggeredSearchServiceFixture : CoreTest<PlexWatchTriggeredSearchService>
    {
        private PlexServerSettings _settings;
        private Series _series;

        [SetUp]
        public void SetUp()
        {
            _settings = new PlexServerSettings
            {
                Host = "localhost",
                TriggerMissingEpisodeSearchOnWatch = true,
                WatchTriggeredSearchCooldownHours = 24
            };

            _series = new Series
            {
                Id = 12,
                Title = "30 Rock",
                Monitored = true
            };
        }

        [Test]
        public void should_queue_refresh_rescan_and_missing_search_for_new_watch_activity()
        {
            Mocker.GetMock<IPlexWatchTriggeredSearchStateRepository>()
                .Setup(x => x.GetByServer(5, It.IsAny<IEnumerable<int>>()))
                .Returns(new Dictionary<int, PlexWatchTriggeredSearchState>());

            Subject.Process(5, _settings, new List<(Series Series, PlexWatchEvent WatchEvent)>
            {
                (_series, new PlexWatchEvent { ViewedAtUtc = DateTime.UtcNow.AddMinutes(-5) })
            });

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(x => x.Push(It.Is<RefreshSeriesCommand>(c => c.SeriesIds.Count == 1 && c.SeriesIds[0] == _series.Id), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(x => x.Push(It.Is<RescanSeriesCommand>(c => c.SeriesId == _series.Id), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(x => x.Push(It.Is<MissingEpisodeSearchCommand>(c => c.SeriesId == _series.Id), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
            Mocker.GetMock<IPlexWatchTriggeredSearchStateRepository>()
                .Verify(x => x.Upsert(It.Is<PlexWatchTriggeredSearchState>(s => s.SeriesId == _series.Id && s.PlexServerDefinitionId == 5 && s.LastTriggeredAtUtc.HasValue && s.LastSeenViewedAtUtc.HasValue)), Times.Once);
        }

        [Test]
        public void should_not_queue_when_cooldown_is_active()
        {
            var now = DateTime.UtcNow;

            Mocker.GetMock<IPlexWatchTriggeredSearchStateRepository>()
                .Setup(x => x.GetByServer(5, It.IsAny<IEnumerable<int>>()))
                .Returns(new Dictionary<int, PlexWatchTriggeredSearchState>
                {
                    {
                        _series.Id, new PlexWatchTriggeredSearchState
                        {
                            SeriesId = _series.Id,
                            PlexServerDefinitionId = 5,
                            LastSeenViewedAtUtc = now.AddHours(-2),
                            LastTriggeredAtUtc = now.AddHours(-1)
                        }
                    }
                });

            Subject.Process(5, _settings, new List<(Series Series, PlexWatchEvent WatchEvent)>
            {
                (_series, new PlexWatchEvent { ViewedAtUtc = now })
            });

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(x => x.Push(It.IsAny<RefreshSeriesCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never);
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(x => x.Push(It.IsAny<RescanSeriesCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never);
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(x => x.Push(It.IsAny<MissingEpisodeSearchCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never);
            Mocker.GetMock<IPlexWatchTriggeredSearchStateRepository>()
                .Verify(x => x.Upsert(It.Is<PlexWatchTriggeredSearchState>(s => s.LastSeenViewedAtUtc == now)), Times.Once);
        }

        [Test]
        public void should_skip_unmonitored_series_but_record_watch_state()
        {
            _series.Monitored = false;
            var viewedAtUtc = DateTime.UtcNow;

            Mocker.GetMock<IPlexWatchTriggeredSearchStateRepository>()
                .Setup(x => x.GetByServer(5, It.IsAny<IEnumerable<int>>()))
                .Returns(new Dictionary<int, PlexWatchTriggeredSearchState>());

            Subject.Process(5, _settings, new List<(Series Series, PlexWatchEvent WatchEvent)>
            {
                (_series, new PlexWatchEvent { ViewedAtUtc = viewedAtUtc })
            });

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(x => x.Push(It.IsAny<RefreshSeriesCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never);
            Mocker.GetMock<IPlexWatchTriggeredSearchStateRepository>()
                .Verify(x => x.Upsert(It.Is<PlexWatchTriggeredSearchState>(s => s.LastSeenViewedAtUtc == viewedAtUtc && !s.LastTriggeredAtUtc.HasValue)), Times.Once);
        }
    }
}
