using System;
using System.Collections.Generic;

namespace Sonarr.Api.V3.Dashboard
{
    public class DashboardResource
    {
        public DashboardTotalsResource Totals { get; set; }
        public DashboardWatchResource Watch { get; set; }
        public List<DashboardGrowthPointResource> Growth { get; set; }
        public List<DashboardBreakdownResource> Networks { get; set; }
        public List<DashboardBreakdownResource> Qualities { get; set; }
        public List<DashboardSeriesEntryResource> PopularSeries { get; set; }
        public List<DashboardSeriesEntryResource> RecentSeries { get; set; }
        public List<DashboardSeriesEntryResource> UpcomingSeries { get; set; }
    }

    public class DashboardTotalsResource
    {
        public int SeriesCount { get; set; }
        public int EpisodeCount { get; set; }
        public int TotalEpisodeCount { get; set; }
        public int EpisodeFileCount { get; set; }
        public long TotalSizeOnDisk { get; set; }
        public long AverageEpisodeFileSize { get; set; }
        public decimal DownloadedPercentage { get; set; }
        public int MonitoredSeriesCount { get; set; }
        public int ContinuingSeriesCount { get; set; }
        public int EndedSeriesCount { get; set; }
        public int UpcomingSeriesCount { get; set; }
        public int MissingEpisodeCount { get; set; }
        public int UpcomingEpisodesNext7Days { get; set; }
        public int UpcomingEpisodesNext30Days { get; set; }
    }

    public class DashboardWatchResource
    {
        public int WatchedSeriesCount { get; set; }
        public int NeverWatchedSeriesCount { get; set; }
        public int ViewsLast30Days { get; set; }
        public int ViewsPrevious30Days { get; set; }
        public int ViewsAllTime { get; set; }
    }

    public class DashboardGrowthPointResource
    {
        public DateTime Date { get; set; }
        public int SeriesCount { get; set; }
        public int TotalEpisodeCount { get; set; }
    }

    public class DashboardBreakdownResource
    {
        public string Label { get; set; }
        public int Count { get; set; }
        public long Size { get; set; }
    }

    public class DashboardSeriesEntryResource
    {
        public int SeriesId { get; set; }
        public string Title { get; set; }
        public string TitleSlug { get; set; }
        public string Network { get; set; }
        public string Status { get; set; }
        public DateTime? Added { get; set; }
        public DateTime? NextAiring { get; set; }
        public int EpisodeCount { get; set; }
        public int EpisodeFileCount { get; set; }
        public int ViewsLast30Days { get; set; }
        public int ViewsAllTime { get; set; }
        public DateTime? LastViewedAt { get; set; }
        public long SizeOnDisk { get; set; }
    }
}
