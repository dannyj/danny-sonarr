using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(229)]
    public class add_plex_watch_triggered_search_state : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("PlexWatchTriggeredSearchState")
                .WithColumn("SeriesId").AsInt32().NotNullable()
                .WithColumn("PlexServerDefinitionId").AsInt32().NotNullable()
                .WithColumn("LastSeenViewedAtUtc").AsDateTime().Nullable()
                .WithColumn("LastTriggeredAtUtc").AsDateTime().Nullable()
                .WithColumn("CreatedAtUtc").AsDateTime().NotNullable()
                .WithColumn("UpdatedAtUtc").AsDateTime().NotNullable();

            Create.Index().OnTable("PlexWatchTriggeredSearchState")
                .OnColumn("SeriesId").Ascending()
                .OnColumn("PlexServerDefinitionId").Ascending()
                .WithOptions().Unique();
        }
    }
}
