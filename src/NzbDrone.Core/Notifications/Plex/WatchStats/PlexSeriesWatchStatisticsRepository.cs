using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexSeriesWatchStatisticsRepository : IBasicRepository<PlexSeriesWatchStatistic>
    {
        void ReplaceForServer(int plexServerDefinitionId, IList<PlexSeriesWatchStatistic> statistics);
        void UpsertDeltas(IList<PlexSeriesWatchStatisticDelta> statistics);
        Dictionary<int, PlexSeriesWatchStatisticsAggregate> GetAggregates();
        PlexSeriesWatchStatisticsAggregate GetAggregate(int seriesId);
    }

    public class PlexSeriesWatchStatisticsRepository : BasicRepository<PlexSeriesWatchStatistic>, IPlexSeriesWatchStatisticsRepository
    {
        public PlexSeriesWatchStatisticsRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public void ReplaceForServer(int plexServerDefinitionId, IList<PlexSeriesWatchStatistic> statistics)
        {
            Delete(x => x.PlexServerDefinitionId == plexServerDefinitionId);

            if (statistics.Count == 0)
            {
                return;
            }

            InsertMany(statistics);
        }

        public void UpsertDeltas(IList<PlexSeriesWatchStatisticDelta> statistics)
        {
            if (statistics.Count == 0)
            {
                return;
            }

            const string sql = @"INSERT INTO ""PlexSeriesWatchStatistics""
(""SeriesId"", ""PlexServerDefinitionId"", ""ViewedOn"", ""ViewCount"", ""LastViewedAtUtc"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
VALUES
(@SeriesId, @PlexServerDefinitionId, @ViewedOn, @ViewCount, @LastViewedAtUtc, @CreatedAtUtc, @UpdatedAtUtc)
ON CONFLICT(""SeriesId"", ""PlexServerDefinitionId"", ""ViewedOn"")
DO UPDATE SET
    ""ViewCount"" = ""PlexSeriesWatchStatistics"".""ViewCount"" + excluded.""ViewCount"",
    ""LastViewedAtUtc"" = CASE
        WHEN ""PlexSeriesWatchStatistics"".""LastViewedAtUtc"" IS NULL THEN excluded.""LastViewedAtUtc""
        WHEN excluded.""LastViewedAtUtc"" IS NULL THEN ""PlexSeriesWatchStatistics"".""LastViewedAtUtc""
        WHEN excluded.""LastViewedAtUtc"" > ""PlexSeriesWatchStatistics"".""LastViewedAtUtc"" THEN excluded.""LastViewedAtUtc""
        ELSE ""PlexSeriesWatchStatistics"".""LastViewedAtUtc""
    END,
    ""UpdatedAtUtc"" = excluded.""UpdatedAtUtc"";";

            using (var conn = _database.OpenConnection())
            {
                conn.Execute(sql, statistics);
            }
        }

        public Dictionary<int, PlexSeriesWatchStatisticsAggregate> GetAggregates()
        {
            return QueryAggregates(null).ToDictionary(x => x.SeriesId, x => x);
        }

        public PlexSeriesWatchStatisticsAggregate GetAggregate(int seriesId)
        {
            return QueryAggregates(seriesId).SingleOrDefault() ?? new PlexSeriesWatchStatisticsAggregate
            {
                SeriesId = seriesId
            };
        }

        private List<PlexSeriesWatchStatisticsAggregate> QueryAggregates(int? seriesId)
        {
            var today = DateTime.UtcNow.Date;
            var last30Start = today.AddDays(-29);
            var previous30Start = today.AddDays(-59);

            var sql = @"SELECT ""SeriesId"" AS SeriesId,
                               SUM(CASE WHEN ""ViewedOn"" >= @last30Start THEN ""ViewCount"" ELSE 0 END) AS ViewsLast30Days,
                               SUM(CASE WHEN ""ViewedOn"" >= @previous30Start AND ""ViewedOn"" < @last30Start THEN ""ViewCount"" ELSE 0 END) AS ViewsPrevious30Days,
                               SUM(""ViewCount"") AS ViewsAllTime,
                               MAX(""LastViewedAtUtc"") AS LastViewedAtString
                        FROM ""PlexSeriesWatchStatistics""
                        /**where**/
                        GROUP BY ""SeriesId""";

            var builder = new SqlBuilder(_database.DatabaseType);
            if (seriesId.HasValue)
            {
                builder.Where(@"""SeriesId"" = @seriesId", new { seriesId });
            }

            var template = builder.AddTemplate(sql, new
            {
                last30Start,
                previous30Start
            });

            using (var conn = _database.OpenConnection())
            {
                return conn.Query<PlexSeriesWatchStatisticsAggregate>(template.RawSql, template.Parameters).ToList();
            }
        }
    }
}
