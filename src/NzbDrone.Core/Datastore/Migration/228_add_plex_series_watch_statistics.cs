using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(228)]
    public class add_plex_series_watch_statistics : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("PlexSeriesWatchStatistics")
                .WithColumn("SeriesId").AsInt32().NotNullable()
                .WithColumn("PlexServerDefinitionId").AsInt32().NotNullable()
                .WithColumn("ViewedOn").AsDateTime().NotNullable()
                .WithColumn("ViewCount").AsInt32().NotNullable()
                .WithColumn("LastViewedAtUtc").AsDateTime().Nullable()
                .WithColumn("CreatedAtUtc").AsDateTime().NotNullable()
                .WithColumn("UpdatedAtUtc").AsDateTime().NotNullable();

            Create.Index().OnTable("PlexSeriesWatchStatistics").OnColumn("SeriesId");
            Create.Index().OnTable("PlexSeriesWatchStatistics").OnColumn("ViewedOn");
            Create.Index().OnTable("PlexSeriesWatchStatistics")
                .OnColumn("PlexServerDefinitionId").Ascending()
                .OnColumn("ViewedOn").Ascending();
            Create.Index().OnTable("PlexSeriesWatchStatistics")
                .OnColumn("SeriesId").Ascending()
                .OnColumn("PlexServerDefinitionId").Ascending()
                .OnColumn("ViewedOn").Ascending()
                .WithOptions().Unique();
        }
    }
}
