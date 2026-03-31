using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(233)]
    public class add_dashboard_snapshots : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("DashboardSnapshots")
                .WithColumn("SnapshotDate").AsDateTime().NotNullable()
                .WithColumn("SeriesCount").AsInt32().NotNullable()
                .WithColumn("TotalEpisodeCount").AsInt32().NotNullable()
                .WithColumn("EpisodeFileCount").AsInt32().NotNullable()
                .WithColumn("TotalSizeOnDisk").AsInt64().NotNullable()
                .WithColumn("ViewsAllTime").AsInt32().NotNullable()
                .WithColumn("CreatedAtUtc").AsDateTime().NotNullable()
                .WithColumn("UpdatedAtUtc").AsDateTime().NotNullable();

            Create.Index().OnTable("DashboardSnapshots")
                .OnColumn("SnapshotDate").Ascending()
                .WithOptions().Unique();
        }
    }
}
