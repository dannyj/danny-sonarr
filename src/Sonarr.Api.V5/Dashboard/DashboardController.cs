using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Dashboard;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.SeriesStats;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V5.Dashboard;

[V5ApiController]
public class DashboardController : Controller
{
    private const int PopularSeriesCount = 8;
    private const int RecentSeriesCount = 6;
    private const int UpcomingSeriesCount = 6;

    private readonly ISeriesService _seriesService;
    private readonly ISeriesStatisticsService _seriesStatisticsService;
    private readonly IMediaFileRepository _mediaFileRepository;
    private readonly IDashboardSnapshotService _dashboardSnapshotService;

    public DashboardController(
        ISeriesService seriesService,
        ISeriesStatisticsService seriesStatisticsService,
        IMediaFileRepository mediaFileRepository,
        IDashboardSnapshotService dashboardSnapshotService)
    {
        _seriesService = seriesService;
        _seriesStatisticsService = seriesStatisticsService;
        _mediaFileRepository = mediaFileRepository;
        _dashboardSnapshotService = dashboardSnapshotService;
    }

    [HttpGet]
    [Produces("application/json")]
    public DashboardResource GetDashboard()
    {
        var utcNow = DateTime.UtcNow;
        var today = utcNow.Date;
        var next7Days = today.AddDays(7);
        var next30Days = today.AddDays(30);

        var series = _seriesService.GetAllSeries();
        var statistics = _seriesStatisticsService.SeriesStatistics()
            .ToDictionary(x => x.SeriesId);
        var episodeFiles = _mediaFileRepository.All().ToList();
        var snapshots = _dashboardSnapshotService.GetSnapshots();

        var totals = BuildTotals(series, statistics, episodeFiles, next7Days, next30Days);

        return new DashboardResource
        {
            Totals = totals,
            Watch = BuildWatchSummary(statistics),
            Growth = BuildGrowth(series, statistics, snapshots),
            Networks = BuildNetworkBreakdown(series, statistics),
            Qualities = BuildQualityBreakdown(episodeFiles),
            PopularSeries = BuildPopularSeries(series, statistics),
            RecentSeries = BuildRecentSeries(series, statistics),
            UpcomingSeries = BuildUpcomingSeries(series, statistics, today, next30Days)
        };
    }

    private static DashboardTotalsResource BuildTotals(
        List<NzbDrone.Core.Tv.Series> series,
        Dictionary<int, SeriesStatistics> statistics,
        List<EpisodeFile> episodeFiles,
        DateTime next7Days,
        DateTime next30Days)
    {
        var allStats = statistics.Values.ToList();
        var totalEpisodeFiles = allStats.Sum(s => s.EpisodeFileCount);
        var totalEpisodes = allStats.Sum(s => s.EpisodeCount);
        var totalMonitoredEpisodes = allStats.Sum(s => s.MonitoredEpisodeCount);
        var totalSeriesSize = episodeFiles.Sum(f => f.Size);

        return new DashboardTotalsResource
        {
            SeriesCount = series.Count,
            EpisodeCount = totalEpisodes,
            TotalEpisodeCount = allStats.Sum(s => s.TotalEpisodeCount),
            EpisodeFileCount = totalEpisodeFiles,
            TotalSizeOnDisk = totalSeriesSize,
            AverageEpisodeFileSize = totalEpisodeFiles == 0 ? 0 : totalSeriesSize / totalEpisodeFiles,
            DownloadedPercentage = totalEpisodes == 0 ? 0 : Math.Round((decimal)totalEpisodeFiles / totalEpisodes * 100, 1),
            MonitoredSeriesCount = series.Count(s => s.Monitored),
            ContinuingSeriesCount = series.Count(s => s.Status == SeriesStatusType.Continuing),
            EndedSeriesCount = series.Count(s => s.Status == SeriesStatusType.Ended),
            UpcomingSeriesCount = series.Count(s => s.Status == SeriesStatusType.Upcoming),
            MissingEpisodeCount = Math.Max(totalMonitoredEpisodes - totalEpisodeFiles, 0),
            UpcomingEpisodesNext7Days = allStats.Count(s => s.NextAiring.HasValue && s.NextAiring.Value <= next7Days),
            UpcomingEpisodesNext30Days = allStats.Count(s => s.NextAiring.HasValue && s.NextAiring.Value <= next30Days)
        };
    }

    private static DashboardWatchResource BuildWatchSummary(Dictionary<int, SeriesStatistics> statistics)
    {
        var allStats = statistics.Values.ToList();

        return new DashboardWatchResource
        {
            WatchedSeriesCount = allStats.Count(s => s.WatchedOnPlex),
            NeverWatchedSeriesCount = allStats.Count(s => s.NeverWatchedOnPlex),
            ViewsLast30Days = allStats.Sum(s => s.ViewsLast30Days),
            ViewsPrevious30Days = allStats.Sum(s => s.ViewsPrevious30Days),
            ViewsAllTime = allStats.Sum(s => s.ViewsAllTime)
        };
    }

    private static List<DashboardGrowthPointResource> BuildGrowth(
        List<NzbDrone.Core.Tv.Series> series,
        Dictionary<int, SeriesStatistics> statistics,
        List<DashboardSnapshot> snapshots)
    {
        if (snapshots.Count >= 2)
        {
            return snapshots
                .Select(snapshot => new DashboardGrowthPointResource
                {
                    Date = snapshot.SnapshotDate,
                    SeriesCount = snapshot.SeriesCount,
                    TotalEpisodeCount = snapshot.TotalEpisodeCount
                })
                .ToList();
        }

        var monthlyChanges = series
            .Where(s => s.Added != default)
            .Select(s =>
            {
                statistics.TryGetValue(s.Id, out var seriesStats);

                return new
                {
                    Date = new DateTime(s.Added.Year, s.Added.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalEpisodeCount = seriesStats?.TotalEpisodeCount ?? 0
                };
            })
            .GroupBy(s => s.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var growth = new List<DashboardGrowthPointResource>();
        var runningSeriesCount = 0;
        var runningEpisodeCount = 0;

        foreach (var point in monthlyChanges)
        {
            runningSeriesCount += point.Count();
            runningEpisodeCount += point.Sum(x => x.TotalEpisodeCount);

            growth.Add(new DashboardGrowthPointResource
            {
                Date = point.Key,
                SeriesCount = runningSeriesCount,
                TotalEpisodeCount = runningEpisodeCount
            });
        }

        return growth;
    }

    private static List<DashboardBreakdownResource> BuildNetworkBreakdown(
        List<NzbDrone.Core.Tv.Series> series,
        Dictionary<int, SeriesStatistics> statistics)
    {
        return series
            .GroupBy(s => s.Network.IsNullOrWhiteSpace() ? "Unknown Network" : s.Network.Trim())
            .Select(group => new DashboardBreakdownResource
            {
                Label = group.Key,
                Count = group.Count(),
                Size = group.Sum(s => statistics.TryGetValue(s.Id, out var seriesStats) ? seriesStats.SizeOnDisk : 0)
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .Take(8)
            .ToList();
    }

    private static List<DashboardBreakdownResource> BuildQualityBreakdown(List<EpisodeFile> episodeFiles)
    {
        return episodeFiles
            .GroupBy(file => GetQualityLabel(file.Quality))
            .Select(group => new DashboardBreakdownResource
            {
                Label = group.Key,
                Count = group.Count(),
                Size = group.Sum(file => file.Size)
            })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.Size)
            .Take(8)
            .ToList();
    }

    private static List<DashboardSeriesEntryResource> BuildPopularSeries(
        List<NzbDrone.Core.Tv.Series> series,
        Dictionary<int, SeriesStatistics> statistics)
    {
        return series
            .Select(s => ToSeriesEntryResource(s, statistics))
            .Where(s => s.ViewsAllTime > 0)
            .OrderByDescending(s => s.ViewsAllTime)
            .ThenByDescending(s => s.ViewsLast30Days)
            .ThenBy(s => s.Title)
            .Take(PopularSeriesCount)
            .ToList();
    }

    private static List<DashboardSeriesEntryResource> BuildRecentSeries(
        List<NzbDrone.Core.Tv.Series> series,
        Dictionary<int, SeriesStatistics> statistics)
    {
        return series
            .Where(s => s.Added != default)
            .OrderByDescending(s => s.Added)
            .ThenBy(s => s.SortTitle)
            .Take(RecentSeriesCount)
            .Select(s => ToSeriesEntryResource(s, statistics))
            .ToList();
    }

    private static List<DashboardSeriesEntryResource> BuildUpcomingSeries(
        List<NzbDrone.Core.Tv.Series> series,
        Dictionary<int, SeriesStatistics> statistics,
        DateTime today,
        DateTime next30Days)
    {
        return series
            .Select(s => new
            {
                Series = s,
                Statistics = statistics.TryGetValue(s.Id, out var seriesStats) ? seriesStats : null
            })
            .Where(x => x.Statistics?.NextAiring != null &&
                        x.Statistics.NextAiring.Value >= today &&
                        x.Statistics.NextAiring.Value <= next30Days)
            .OrderBy(x => x.Statistics!.NextAiring)
            .ThenBy(x => x.Series.SortTitle)
            .Take(UpcomingSeriesCount)
            .Select(x => ToSeriesEntryResource(x.Series, statistics))
            .ToList();
    }

    private static DashboardSeriesEntryResource ToSeriesEntryResource(
        NzbDrone.Core.Tv.Series series,
        Dictionary<int, SeriesStatistics> statistics)
    {
        statistics.TryGetValue(series.Id, out var seriesStats);

        return new DashboardSeriesEntryResource
        {
            SeriesId = series.Id,
            Title = series.Title,
            TitleSlug = series.TitleSlug,
            Network = series.Network,
            Status = series.Status.ToString().ToLowerInvariant(),
            Added = series.Added == default ? null : series.Added,
            NextAiring = seriesStats?.NextAiring,
            EpisodeCount = seriesStats?.EpisodeCount ?? 0,
            EpisodeFileCount = seriesStats?.EpisodeFileCount ?? 0,
            ViewsLast30Days = seriesStats?.ViewsLast30Days ?? 0,
            ViewsAllTime = seriesStats?.ViewsAllTime ?? 0,
            LastViewedAt = seriesStats?.LastViewedAt,
            SizeOnDisk = seriesStats?.SizeOnDisk ?? 0
        };
    }

    private static string GetQualityLabel(QualityModel qualityModel)
    {
        var quality = qualityModel?.Quality;

        if (quality == null)
        {
            return "Unknown";
        }

        var qualityDefinition = Quality.DefaultQualityDefinitions
            .FirstOrDefault(q => q.Quality == quality);

        if (qualityDefinition?.GroupName.IsNotNullOrWhiteSpace() == true)
        {
            return qualityDefinition.GroupName;
        }

        return quality.Name;
    }
}
