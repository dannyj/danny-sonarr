using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Notifications.Plex.Server;
using NzbDrone.Core.Notifications.Plex.WatchStats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests.Plex
{
    [TestFixture]
    public class PlexWatchStatsServiceFixture : CoreTest<PlexWatchStatsService>
    {
        private PlexServerSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new PlexServerSettings
            {
                Host = "localhost",
                AuthToken = "token"
            };
        }

        [Test]
        public void should_normalize_episode_watch_history()
        {
            Mocker.GetMock<IPlexWatchStatsProxy>()
                .Setup(x => x.GetHistory(_settings, 0, 100))
                .Returns(new PlexWatchHistoryPage
                {
                    TotalSize = 1,
                    Metadata = new List<PlexWatchHistoryItem>
                    {
                        new()
                        {
                            Type = "episode",
                            ViewedAt = 1710000000,
                            GrandparentTitle = "30 Rock",
                            GrandparentYear = 2006,
                            GrandparentGuid = "tvdb://79488",
                            AccountId = 12,
                            Guid = new List<PlexWatchHistoryGuid>
                            {
                                new() { Id = "imdb://tt0496424" }
                            },
                            Media = new List<PlexWatchHistoryMedia>
                            {
                                new()
                                {
                                    Part = new List<PlexWatchHistoryPart>
                                    {
                                        new() { File = "/tv/30 Rock/Season 01/30 Rock - S01E01.mkv" }
                                    }
                                }
                            }
                        }
                    }
                });

            var result = Subject.GetWatchEvents(_settings, null, TimeSpan.FromHours(48));

            result.Events.Should().HaveCount(1);
            result.Events[0].SeriesTitle.Should().Be("30 Rock");
            result.Events[0].SeriesYear.Should().Be(2006);
            result.Events[0].FilePath.Should().Be("/tv/30 Rock/Season 01/30 Rock - S01E01.mkv");
            result.Events[0].User.Id.Should().Be("12");
            result.Events[0].Guids.Should().Contain(x => x.Source == "tvdb" && x.Value == "79488");
            result.Events[0].Guids.Should().Contain(x => x.Source == "imdb" && x.Value == "tt0496424");
            result.Events[0].EventKey.Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public void should_skip_non_episode_items()
        {
            Mocker.GetMock<IPlexWatchStatsProxy>()
                .Setup(x => x.GetHistory(_settings, 0, 100))
                .Returns(new PlexWatchHistoryPage
                {
                    TotalSize = 1,
                    Metadata = new List<PlexWatchHistoryItem>
                    {
                        new()
                        {
                            Type = "movie",
                            ViewedAt = 1710000000
                        }
                    }
                });

            Subject.GetWatchEvents(_settings, null, TimeSpan.FromHours(48)).Events.Should().BeEmpty();
        }
    }
}
