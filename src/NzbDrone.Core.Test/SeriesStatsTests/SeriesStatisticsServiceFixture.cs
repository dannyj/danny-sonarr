using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Notifications.Plex.WatchStats;
using NzbDrone.Core.SeriesStats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.SeriesStatsTests
{
    [TestFixture]
    public class SeriesStatisticsServiceFixture : CoreTest<SeriesStatisticsService>
    {
        [Test]
        public void should_merge_plex_watch_statistics_for_single_series()
        {
            Mocker.GetMock<ISeriesStatisticsRepository>()
                .Setup(x => x.SeriesStatistics(12))
                .Returns(new List<SeasonStatistics>
                {
                    new()
                    {
                        SeriesId = 12,
                        SeasonNumber = 1,
                        EpisodeCount = 10,
                        EpisodeFileCount = 10,
                        TotalEpisodeCount = 10,
                        MonitoredEpisodeCount = 10,
                        SizeOnDisk = 100
                    }
                });

            Mocker.GetMock<IPlexSeriesWatchStatisticsRepository>()
                .Setup(x => x.GetAggregate(12))
                .Returns(new PlexSeriesWatchStatisticsAggregate
                {
                    SeriesId = 12,
                    ViewsAllTime = 15,
                    ViewsLast30Days = 8,
                    ViewsPrevious30Days = 3,
                    LastViewedAt = DateTime.UtcNow.AddDays(-2)
                });

            var result = Subject.SeriesStatistics(12);

            result.WatchedOnPlex.Should().BeTrue();
            result.NeverWatchedOnPlex.Should().BeFalse();
            result.ViewsAllTime.Should().Be(15);
            result.ViewsLast30Days.Should().Be(8);
            result.ViewsPrevious30Days.Should().Be(3);
            result.ViewTrend.Should().Be(PlexViewTrend.Up);
            result.DaysSinceLastView.Should().Be(2);
        }

        [Test]
        public void should_return_safe_defaults_when_no_plex_watch_statistics_exist()
        {
            Mocker.GetMock<ISeriesStatisticsRepository>()
                .Setup(x => x.SeriesStatistics(15))
                .Returns(new List<SeasonStatistics>());

            Mocker.GetMock<IPlexSeriesWatchStatisticsRepository>()
                .Setup(x => x.GetAggregate(15))
                .Returns(new PlexSeriesWatchStatisticsAggregate
                {
                    SeriesId = 15
                });

            var result = Subject.SeriesStatistics(15);

            result.WatchedOnPlex.Should().BeFalse();
            result.NeverWatchedOnPlex.Should().BeTrue();
            result.ViewsAllTime.Should().Be(0);
            result.ViewTrend.Should().Be(PlexViewTrend.None);
            result.LastViewedAt.Should().NotHaveValue();
            result.DaysSinceLastView.Should().NotHaveValue();
        }
    }
}
