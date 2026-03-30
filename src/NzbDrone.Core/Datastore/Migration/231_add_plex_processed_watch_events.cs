using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(231)]
    public class add_plex_processed_watch_events : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("PlexProcessedWatchEvents")
                .WithColumn("PlexServerDefinitionId").AsInt32().NotNullable()
                .WithColumn("EventKey").AsString().NotNullable()
                .WithColumn("ViewedAtUtc").AsDateTime().NotNullable()
                .WithColumn("ViewedOn").AsDateTime().NotNullable()
                .WithColumn("SeriesId").AsInt32().Nullable()
                .WithColumn("SeriesTitle").AsString().Nullable()
                .WithColumn("FilePath").AsString().Nullable()
                .WithColumn("Matched").AsBoolean().NotNullable()
                .WithColumn("CreatedAtUtc").AsDateTime().NotNullable();

            Create.Index().OnTable("PlexProcessedWatchEvents")
                .OnColumn("PlexServerDefinitionId").Ascending()
                .OnColumn("EventKey").Ascending()
                .WithOptions().Unique();

            Create.Index().OnTable("PlexProcessedWatchEvents")
                .OnColumn("PlexServerDefinitionId").Ascending()
                .OnColumn("ViewedAtUtc").Ascending();

            Create.Index().OnTable("PlexProcessedWatchEvents")
                .OnColumn("PlexServerDefinitionId").Ascending()
                .OnColumn("SeriesId").Ascending()
                .OnColumn("ViewedOn").Ascending();
        }
    }
}
