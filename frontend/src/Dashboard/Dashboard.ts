export interface DashboardTotals {
  seriesCount: number;
  episodeCount: number;
  totalEpisodeCount: number;
  episodeFileCount: number;
  totalSizeOnDisk: number;
  averageEpisodeFileSize: number;
  downloadedPercentage: number;
  monitoredSeriesCount: number;
  continuingSeriesCount: number;
  endedSeriesCount: number;
  upcomingSeriesCount: number;
  missingEpisodeCount: number;
  upcomingEpisodesNext7Days: number;
  upcomingEpisodesNext30Days: number;
}

export interface DashboardWatch {
  watchedSeriesCount: number;
  neverWatchedSeriesCount: number;
  viewsLast30Days: number;
  viewsPrevious30Days: number;
  viewsAllTime: number;
}

export interface DashboardGrowthPoint {
  date: string;
  seriesCount: number;
  totalEpisodeCount: number;
}

export interface DashboardBreakdown {
  label: string;
  count: number;
  size: number;
}

export interface DashboardSeriesEntry {
  seriesId: number;
  title: string;
  titleSlug: string;
  network: string;
  status: string;
  added?: string;
  nextAiring?: string;
  episodeCount: number;
  episodeFileCount: number;
  viewsLast30Days: number;
  viewsAllTime: number;
  lastViewedAt?: string;
  sizeOnDisk: number;
}

interface Dashboard {
  totals: DashboardTotals;
  watch: DashboardWatch;
  growth: DashboardGrowthPoint[];
  networks: DashboardBreakdown[];
  qualities: DashboardBreakdown[];
  popularSeries: DashboardSeriesEntry[];
  recentSeries: DashboardSeriesEntry[];
  upcomingSeries: DashboardSeriesEntry[];
}

export default Dashboard;
