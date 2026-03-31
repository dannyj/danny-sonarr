using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Dashboard
{
    public interface IDashboardSnapshotRepository : IBasicRepository<DashboardSnapshot>
    {
        List<DashboardSnapshot> GetAllOrdered();
        DashboardSnapshot GetLatest();
        void UpsertSnapshot(DashboardSnapshot snapshot);
    }

    public class DashboardSnapshotRepository : BasicRepository<DashboardSnapshot>, IDashboardSnapshotRepository
    {
        public DashboardSnapshotRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<DashboardSnapshot> GetAllOrdered()
        {
            const string sql = @"SELECT * FROM ""DashboardSnapshots"" ORDER BY ""SnapshotDate"" ASC";

            using (var conn = _database.OpenConnection())
            {
                return conn.Query<DashboardSnapshot>(sql).ToList();
            }
        }

        public DashboardSnapshot GetLatest()
        {
            const string sql = @"SELECT * FROM ""DashboardSnapshots"" ORDER BY ""SnapshotDate"" DESC LIMIT 1";

            using (var conn = _database.OpenConnection())
            {
                return conn.Query<DashboardSnapshot>(sql).FirstOrDefault();
            }
        }

        public void UpsertSnapshot(DashboardSnapshot snapshot)
        {
            const string sql = @"INSERT INTO ""DashboardSnapshots""
(""SnapshotDate"", ""SeriesCount"", ""TotalEpisodeCount"", ""EpisodeFileCount"", ""TotalSizeOnDisk"", ""ViewsAllTime"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
VALUES
(@SnapshotDate, @SeriesCount, @TotalEpisodeCount, @EpisodeFileCount, @TotalSizeOnDisk, @ViewsAllTime, @CreatedAtUtc, @UpdatedAtUtc)
ON CONFLICT(""SnapshotDate"")
DO UPDATE SET
    ""SeriesCount"" = excluded.""SeriesCount"",
    ""TotalEpisodeCount"" = excluded.""TotalEpisodeCount"",
    ""EpisodeFileCount"" = excluded.""EpisodeFileCount"",
    ""TotalSizeOnDisk"" = excluded.""TotalSizeOnDisk"",
    ""ViewsAllTime"" = excluded.""ViewsAllTime"",
    ""UpdatedAtUtc"" = excluded.""UpdatedAtUtc"";";

            using (var conn = _database.OpenConnection())
            {
                conn.Execute(sql, snapshot);
            }
        }
    }
}
