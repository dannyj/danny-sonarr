import moment from 'moment';
import React, { ReactNode, useMemo } from 'react';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import NoSeries from 'Series/NoSeries';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import getRelativeDate from 'Utilities/Date/getRelativeDate';
import formatBytes from 'Utilities/Number/formatBytes';
import Dashboard, {
  DashboardBreakdown,
  DashboardGrowthPoint,
  DashboardSeriesEntry,
} from './Dashboard';
import styles from './DashboardPage.css';

const PATH = '/dashboard';

function DashboardPage() {
  const uiSettings = useUiSettingsValues();
  const { data, isLoading, isError, error } = useApiQuery<Dashboard>({
    path: PATH,
  });

  const growthChart = useMemo(() => {
    return buildGrowthChart(data?.growth ?? []);
  }, [data?.growth]);

  if (isError) {
    return (
      <PageContent title="Dashboard">
        <PageContentBody className={styles.pageBody}>
          <div className={styles.errorPanel}>
            <div className={styles.errorTitle}>Dashboard unavailable</div>
            <div className={styles.errorMessage}>
              {error?.message ?? 'Failed to load dashboard data.'}
            </div>
          </div>
        </PageContentBody>
      </PageContent>
    );
  }

  if (isLoading || !data) {
    return (
      <PageContent title="Dashboard">
        <PageContentBody className={styles.pageBody}>
          <div className={styles.loadingPanel}>
            <LoadingIndicator />
          </div>
        </PageContentBody>
      </PageContent>
    );
  }

  if (!data.totals.seriesCount) {
    return (
      <PageContent title="Dashboard">
        <PageContentBody className={styles.pageBody}>
          <NoSeries totalItems={0} />
        </PageContentBody>
      </PageContent>
    );
  }

  const viewDelta = data.watch.viewsLast30Days - data.watch.viewsPrevious30Days;
  const watchedRatio = data.totals.seriesCount
    ? Math.round((data.watch.watchedSeriesCount / data.totals.seriesCount) * 100)
    : 0;

  return (
    <PageContent title="Dashboard">
      <PageContentBody
        className={styles.pageBody}
        innerClassName={styles.pageInnerBody}
      >
        <section className={styles.hero}>
          <div className={styles.heroGlow} />

          <div className={styles.heroHeader}>
            <div>
              <div className={styles.eyebrow}>Private analytics</div>
              <h1 className={styles.heroTitle}>Library dashboard</h1>
              <p className={styles.heroCopy}>
                A live view of collection growth, storage, quality mix, network
                spread, and Plex watch activity.
              </p>
            </div>

            <div className={styles.heroStats}>
              <HeroStat
                label="Downloaded"
                value={`${data.totals.downloadedPercentage}%`}
              />
              <HeroStat label="Watched on Plex" value={`${watchedRatio}%`} />
              <HeroStat
                label="30-day view delta"
                value={formatSignedNumber(viewDelta)}
              />
            </div>
          </div>
        </section>

        <section className={styles.metricsGrid}>
          <MetricCard
            label="Series"
            value={formatNumber(data.totals.seriesCount)}
            accentClassName={styles.blue}
            detail={`${data.totals.monitoredSeriesCount} monitored`}
          />
          <MetricCard
            label="Episodes"
            value={formatNumber(data.totals.totalEpisodeCount)}
            accentClassName={styles.teal}
            detail={`${formatNumber(data.totals.episodeFileCount)} files on disk`}
          />
          <MetricCard
            label="Storage"
            value={formatBytes(data.totals.totalSizeOnDisk)}
            accentClassName={styles.gold}
            detail={`${formatBytes(data.totals.averageEpisodeFileSize)} avg / file`}
          />
          <MetricCard
            label="Views"
            value={formatNumber(data.watch.viewsAllTime)}
            accentClassName={styles.rose}
            detail={`${formatNumber(data.watch.viewsLast30Days)} in the last 30 days`}
          />
        </section>

        <section className={styles.mainGrid}>
          <Panel
            className={styles.growthPanel}
            title="Library growth"
            subtitle="Cumulative series and episode counts by the month they were added"
          >
            <div className={styles.chartShell}>
              <svg viewBox="0 0 720 280" className={styles.chart}>
                <defs>
                  <linearGradient
                    id="dashboardSeriesFill"
                    x1="0"
                    y1="0"
                    x2="0"
                    y2="1"
                  >
                    <stop offset="0%" stopColor="rgba(75, 163, 255, 0.4)" />
                    <stop offset="100%" stopColor="rgba(75, 163, 255, 0.02)" />
                  </linearGradient>
                </defs>

                {growthChart.gridLines.map((y) => (
                  <line
                    key={y}
                    x1="0"
                    x2="720"
                    y1={y}
                    y2={y}
                    className={styles.chartGridLine}
                  />
                ))}

                {growthChart.seriesAreaPath ? (
                  <path
                    d={growthChart.seriesAreaPath}
                    className={styles.seriesArea}
                  />
                ) : null}

                {growthChart.episodePath ? (
                  <path
                    d={growthChart.episodePath}
                    className={styles.episodePath}
                  />
                ) : null}

                {growthChart.seriesPath ? (
                  <path
                    d={growthChart.seriesPath}
                    className={styles.seriesPath}
                  />
                ) : null}

                {growthChart.points.map((point) => (
                  <circle
                    key={point.key}
                    cx={point.x}
                    cy={point.seriesY}
                    r="4"
                    className={styles.seriesPoint}
                  />
                ))}
              </svg>

              <div className={styles.chartLegend}>
                <LegendSwatch className={styles.seriesLegend} label="Series" />
                <LegendSwatch
                  className={styles.episodeLegend}
                  label="Episodes"
                />
              </div>
            </div>

            <div className={styles.timelineLabels}>
              {data.growth.slice(-6).map((point) => (
                <div key={point.date} className={styles.timelineLabel}>
                  {moment(point.date).format('MMM YY')}
                </div>
              ))}
            </div>
          </Panel>

          <Panel
            title="Watch signal"
            subtitle="Plex-derived engagement across the tracked library"
          >
            <div className={styles.watchGrid}>
              <MiniMetric
                label="Watched series"
                value={formatNumber(data.watch.watchedSeriesCount)}
              />
              <MiniMetric
                label="Never watched"
                value={formatNumber(data.watch.neverWatchedSeriesCount)}
              />
              <MiniMetric
                label="Last 30 days"
                value={formatNumber(data.watch.viewsLast30Days)}
              />
              <MiniMetric
                label="Previous 30 days"
                value={formatNumber(data.watch.viewsPrevious30Days)}
              />
            </div>

            <div className={styles.watchTrendCard}>
              <div className={styles.watchTrendLabel}>Momentum</div>
              <div className={styles.watchTrendValue}>
                {formatSignedNumber(viewDelta)} views
              </div>
              <div className={styles.watchTrendCopy}>
                Compared with the previous 30-day window.
              </div>
            </div>

            <div className={styles.statusBarGroup}>
              <StatusBar
                label="Continuing"
                value={data.totals.continuingSeriesCount}
                total={data.totals.seriesCount}
                toneClassName={styles.toneBlue}
              />
              <StatusBar
                label="Ended"
                value={data.totals.endedSeriesCount}
                total={data.totals.seriesCount}
                toneClassName={styles.toneSlate}
              />
              <StatusBar
                label="Upcoming"
                value={data.totals.upcomingSeriesCount}
                total={data.totals.seriesCount}
                toneClassName={styles.toneGold}
              />
            </div>
          </Panel>
        </section>

        <section className={styles.secondaryGrid}>
          <Panel
            title="Network mix"
            subtitle="Where the library is concentrated"
          >
            <BreakdownList
              items={data.networks}
              valueLabel={(item) => `${formatNumber(item.count)} series`}
            />
          </Panel>

          <Panel
            title="Quality mix"
            subtitle="Episode file distribution by detected quality"
          >
            <BreakdownList
              items={data.qualities}
              valueLabel={(item) =>
                `${formatNumber(item.count)} files • ${formatBytes(item.size)}`
              }
            />
          </Panel>
        </section>

        <section className={styles.tableGrid}>
          <Panel
            title="Most viewed series"
            subtitle="Sorted by all-time Plex views"
          >
            <SeriesList
              items={data.popularSeries}
              secondary={(series) =>
                `${formatNumber(series.viewsAllTime)} all-time • ${formatNumber(series.viewsLast30Days)} last 30d`
              }
              meta={(series) =>
                series.lastViewedAt
                  ? `Last viewed ${formatDashboardDate(series.lastViewedAt, uiSettings)}`
                  : 'No recent view'
              }
            />
          </Panel>

          <Panel title="Recently added" subtitle="Fresh arrivals in the library">
            <SeriesList
              items={data.recentSeries}
              secondary={(series) =>
                `${formatNumber(series.episodeCount)} episodes • ${formatBytes(series.sizeOnDisk)}`
              }
              meta={(series) =>
                series.added
                  ? `Added ${formatDashboardDate(series.added, uiSettings)}`
                  : 'Added date unavailable'
              }
            />
          </Panel>

          <Panel
            title="Upcoming soon"
            subtitle="Series with an upcoming air date in the next 30 days"
          >
            <SeriesList
              items={data.upcomingSeries}
              secondary={(series) =>
                `${formatNumber(series.episodeFileCount)}/${formatNumber(series.episodeCount)} available`
              }
              meta={(series) =>
                series.nextAiring
                  ? `Next airing ${formatDashboardDate(series.nextAiring, uiSettings)}`
                  : 'No next airing'
              }
            />
          </Panel>
        </section>

        <section className={styles.footerGrid}>
          <InfoPill
            label="Missing monitored episodes"
            value={formatNumber(data.totals.missingEpisodeCount)}
          />
          <InfoPill
            label="Series airing in 7 days"
            value={formatNumber(data.totals.upcomingEpisodesNext7Days)}
          />
          <InfoPill
            label="Series airing in 30 days"
            value={formatNumber(data.totals.upcomingEpisodesNext30Days)}
          />
        </section>
      </PageContentBody>
    </PageContent>
  );
}

export default DashboardPage;

function HeroStat({ label, value }: { label: string; value: string }) {
  return (
    <div className={styles.heroStat}>
      <div className={styles.heroStatLabel}>{label}</div>
      <div className={styles.heroStatValue}>{value}</div>
    </div>
  );
}

function MetricCard({
  label,
  value,
  detail,
  accentClassName,
}: {
  label: string;
  value: string;
  detail: string;
  accentClassName: string;
}) {
  return (
    <div className={`${styles.metricCard} ${accentClassName}`}>
      <div className={styles.metricLabel}>{label}</div>
      <div className={styles.metricValue}>{value}</div>
      <div className={styles.metricDetail}>{detail}</div>
    </div>
  );
}

function Panel({
  title,
  subtitle,
  children,
  className,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`${styles.panel} ${className ?? ''}`.trim()}>
      <div className={styles.panelHeader}>
        <div className={styles.panelTitle}>{title}</div>
        <div className={styles.panelSubtitle}>{subtitle}</div>
      </div>

      {children}
    </section>
  );
}

function MiniMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className={styles.miniMetric}>
      <div className={styles.miniMetricLabel}>{label}</div>
      <div className={styles.miniMetricValue}>{value}</div>
    </div>
  );
}

function StatusBar({
  label,
  value,
  total,
  toneClassName,
}: {
  label: string;
  value: number;
  total: number;
  toneClassName: string;
}) {
  const width = total ? Math.max((value / total) * 100, 4) : 0;

  return (
    <div className={styles.statusBarRow}>
      <div className={styles.statusBarHeader}>
        <span>{label}</span>
        <span>{formatNumber(value)}</span>
      </div>

      <div className={styles.statusBarTrack}>
        <div
          className={`${styles.statusBarFill} ${toneClassName}`}
          style={{ width: `${Math.min(width, 100)}%` }}
        />
      </div>
    </div>
  );
}

function BreakdownList({
  items,
  valueLabel,
}: {
  items: DashboardBreakdown[];
  valueLabel: (item: DashboardBreakdown) => string;
}) {
  const maxCount = Math.max(...items.map((item) => item.count), 1);

  return (
    <div className={styles.breakdownList}>
      {items.map((item) => (
        <div key={item.label} className={styles.breakdownRow}>
          <div className={styles.breakdownHeader}>
            <span className={styles.breakdownLabel}>{item.label}</span>
            <span className={styles.breakdownValue}>{valueLabel(item)}</span>
          </div>

          <div className={styles.breakdownTrack}>
            <div
              className={styles.breakdownFill}
              style={{ width: `${(item.count / maxCount) * 100}%` }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}

function SeriesList({
  items,
  secondary,
  meta,
}: {
  items: DashboardSeriesEntry[];
  secondary: (item: DashboardSeriesEntry) => string;
  meta: (item: DashboardSeriesEntry) => string;
}) {
  if (!items.length) {
    return <div className={styles.emptyState}>No data available yet.</div>;
  }

  return (
    <div className={styles.seriesList}>
      {items.map((item) => (
        <Link
          key={item.seriesId}
          className={styles.seriesRow}
          to={`/series/${item.titleSlug}`}
        >
          <div className={styles.seriesPrimary}>
            <div className={styles.seriesTitle}>{item.title}</div>
            <div className={styles.seriesSecondary}>{secondary(item)}</div>
          </div>

          <div className={styles.seriesMeta}>
            <div className={styles.seriesNetwork}>
              {item.network || 'Unknown Network'}
            </div>
            <div className={styles.seriesMetaText}>{meta(item)}</div>
          </div>
        </Link>
      ))}
    </div>
  );
}

function InfoPill({ label, value }: { label: string; value: string }) {
  return (
    <div className={styles.infoPill}>
      <div className={styles.infoPillLabel}>{label}</div>
      <div className={styles.infoPillValue}>{value}</div>
    </div>
  );
}

function LegendSwatch({
  className,
  label,
}: {
  className: string;
  label: string;
}) {
  return (
    <div className={styles.legendItem}>
      <span className={`${styles.legendSwatch} ${className}`} />
      <span>{label}</span>
    </div>
  );
}

function formatNumber(value: number) {
  return Intl.NumberFormat().format(value);
}

function formatSignedNumber(value: number) {
  return `${value > 0 ? '+' : ''}${formatNumber(value)}`;
}

function formatDashboardDate(date: string, uiSettings?: {
  shortDateFormat: string;
  showRelativeDates: boolean;
  timeFormat: string;
  timeZone: string;
}) {
  if (!uiSettings) {
    return moment(date).format('ll');
  }

  return getRelativeDate({
    date,
    shortDateFormat: uiSettings.shortDateFormat,
    showRelativeDates: uiSettings.showRelativeDates,
    timeFormat: uiSettings.timeFormat,
    timeZone: uiSettings.timeZone,
    includeTime: false,
  });
}

function buildGrowthChart(points: DashboardGrowthPoint[]) {
  if (!points.length) {
    return {
      points: [],
      gridLines: [48, 112, 176, 240],
      seriesPath: '',
      seriesAreaPath: '',
      episodePath: '',
    };
  }

  const width = 720;
  const height = 280;
  const paddingX = 18;
  const paddingY = 20;
  const innerWidth = width - paddingX * 2;
  const innerHeight = height - paddingY * 2;
  const maxSeries = Math.max(...points.map((point) => point.seriesCount), 1);
  const maxEpisodes = Math.max(
    ...points.map((point) => point.totalEpisodeCount),
    1
  );

  const mappedPoints = points.map((point, index) => {
    const x =
      paddingX +
      (points.length === 1 ? innerWidth / 2 : (index / (points.length - 1)) * innerWidth);
    const seriesY =
      height - paddingY - (point.seriesCount / maxSeries) * innerHeight;
    const episodeY =
      height -
      paddingY -
      (point.totalEpisodeCount / maxEpisodes) * innerHeight;

    return {
      key: point.date,
      x,
      seriesY,
      episodeY,
    };
  });

  const seriesPath = toLinePath(mappedPoints, 'seriesY');
  const episodePath = toLinePath(mappedPoints, 'episodeY');
  const seriesAreaPath = seriesPath
    ? `${seriesPath} L ${mappedPoints[mappedPoints.length - 1].x} ${
        height - paddingY
      } L ${mappedPoints[0].x} ${height - paddingY} Z`
    : '';

  return {
    points: mappedPoints,
    gridLines: [48, 112, 176, 240],
    seriesPath,
    seriesAreaPath,
    episodePath,
  };
}

function toLinePath(
  points: Array<{ x: number; seriesY: number; episodeY: number }>,
  key: 'seriesY' | 'episodeY'
) {
  return points
    .map((point, index) => {
      const prefix = index === 0 ? 'M' : 'L';
      return `${prefix} ${point.x} ${point[key]}`;
    })
    .join(' ');
}
