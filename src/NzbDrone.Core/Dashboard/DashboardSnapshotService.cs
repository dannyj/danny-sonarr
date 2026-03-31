using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Notifications.Plex.WatchStats;
using NzbDrone.Core.SeriesStats;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Dashboard
{
    public interface IDashboardSnapshotService
    {
        List<DashboardSnapshot> GetSnapshots();
        DashboardSnapshot CaptureSnapshot();
    }

    public class DashboardSnapshotService : IDashboardSnapshotService, IExecute<CaptureDashboardSnapshotCommand>
    {
        private readonly ISeriesService _seriesService;
        private readonly ISeriesStatisticsService _seriesStatisticsService;
        private readonly IPlexSeriesWatchStatisticsRepository _plexSeriesWatchStatisticsRepository;
        private readonly IDashboardSnapshotRepository _dashboardSnapshotRepository;
        private readonly Logger _logger;

        public DashboardSnapshotService(ISeriesService seriesService,
                                        ISeriesStatisticsService seriesStatisticsService,
                                        IPlexSeriesWatchStatisticsRepository plexSeriesWatchStatisticsRepository,
                                        IDashboardSnapshotRepository dashboardSnapshotRepository,
                                        Logger logger)
        {
            _seriesService = seriesService;
            _seriesStatisticsService = seriesStatisticsService;
            _plexSeriesWatchStatisticsRepository = plexSeriesWatchStatisticsRepository;
            _dashboardSnapshotRepository = dashboardSnapshotRepository;
            _logger = logger;
        }

        public List<DashboardSnapshot> GetSnapshots()
        {
            return _dashboardSnapshotRepository.GetAllOrdered();
        }

        public DashboardSnapshot CaptureSnapshot()
        {
            var utcNow = DateTime.UtcNow;
            var series = _seriesService.GetAllSeries();
            var statistics = _seriesStatisticsService.SeriesStatistics();
            var watchStatistics = _plexSeriesWatchStatisticsRepository.GetAggregates();

            var snapshot = new DashboardSnapshot
            {
                SnapshotDate = utcNow.Date,
                SeriesCount = series.Count,
                TotalEpisodeCount = 0,
                EpisodeFileCount = 0,
                TotalSizeOnDisk = 0,
                ViewsAllTime = 0,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            };

            foreach (var seriesStatistic in statistics)
            {
                snapshot.TotalEpisodeCount += seriesStatistic.TotalEpisodeCount;
                snapshot.EpisodeFileCount += seriesStatistic.EpisodeFileCount;
                snapshot.TotalSizeOnDisk += seriesStatistic.SizeOnDisk;

                if (watchStatistics.TryGetValue(seriesStatistic.SeriesId, out var watchStatistic))
                {
                    snapshot.ViewsAllTime += watchStatistic.ViewsAllTime;
                }
            }

            _dashboardSnapshotRepository.UpsertSnapshot(snapshot);

            _logger.Info("Captured dashboard snapshot for {0:d}: {1} series, {2} episodes, {3} files, {4} views",
                snapshot.SnapshotDate,
                snapshot.SeriesCount,
                snapshot.TotalEpisodeCount,
                snapshot.EpisodeFileCount,
                snapshot.ViewsAllTime);

            return snapshot;
        }

        public void Execute(CaptureDashboardSnapshotCommand message)
        {
            CaptureSnapshot();
        }
    }
}
