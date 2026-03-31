namespace Sonarr.Api.V5.Dashboard;

public class DashboardResource
{
    public DashboardTotalsResource Totals { get; set; } = new();
    public DashboardWatchResource Watch { get; set; } = new();
    public List<DashboardGrowthPointResource> Growth { get; set; } = [];
    public List<DashboardBreakdownResource> Networks { get; set; } = [];
    public List<DashboardBreakdownResource> Qualities { get; set; } = [];
    public List<DashboardSeriesEntryResource> PopularSeries { get; set; } = [];
    public List<DashboardSeriesEntryResource> RecentSeries { get; set; } = [];
    public List<DashboardSeriesEntryResource> UpcomingSeries { get; set; } = [];
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
    public global::System.DateTime Date { get; set; }
    public int SeriesCount { get; set; }
    public int TotalEpisodeCount { get; set; }
}

public class DashboardBreakdownResource
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public long Size { get; set; }
}

public class DashboardSeriesEntryResource
{
    public int SeriesId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSlug { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public global::System.DateTime? Added { get; set; }
    public global::System.DateTime? NextAiring { get; set; }
    public int EpisodeCount { get; set; }
    public int EpisodeFileCount { get; set; }
    public int ViewsLast30Days { get; set; }
    public int ViewsAllTime { get; set; }
    public global::System.DateTime? LastViewedAt { get; set; }
    public long SizeOnDisk { get; set; }
}
