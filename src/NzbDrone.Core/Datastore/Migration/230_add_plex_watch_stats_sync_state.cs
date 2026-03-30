using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(230)]
    public class add_plex_watch_stats_sync_state : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("PlexWatchStatsSyncState")
                .WithColumn("PlexServerDefinitionId").AsInt32().NotNullable()
                .WithColumn("LastSuccessfulViewedAtUtc").AsDateTime().Nullable()
                .WithColumn("LastSuccessfulEventKey").AsString().Nullable()
                .WithColumn("LastRunStartedAtUtc").AsDateTime().Nullable()
                .WithColumn("LastRunCompletedAtUtc").AsDateTime().Nullable()
                .WithColumn("LastRunStatus").AsString().Nullable()
                .WithColumn("LastRunMessage").AsString().Nullable()
                .WithColumn("FullResyncRequired").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("CreatedAtUtc").AsDateTime().NotNullable()
                .WithColumn("UpdatedAtUtc").AsDateTime().NotNullable();

            Create.Index().OnTable("PlexWatchStatsSyncState")
                .OnColumn("PlexServerDefinitionId").Ascending()
                .WithOptions().Unique();
        }
    }
}
