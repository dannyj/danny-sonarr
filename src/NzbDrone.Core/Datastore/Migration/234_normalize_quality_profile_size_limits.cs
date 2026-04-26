using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using FluentMigrator;
using Newtonsoft.Json;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(234)]
    public class normalize_quality_profile_size_limits : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(NormalizeProfiles);
        }

        private void NormalizeProfiles(IDbConnection conn, IDbTransaction tran)
        {
            var profiles = GetProfiles(conn, tran);
            var changedProfiles = new List<Profile234>();

            foreach (var profile in profiles)
            {
                var changed = false;

                foreach (var item in profile.Items)
                {
                    changed |= NormalizeItem(item);

                    foreach (var groupedItem in item.Items)
                    {
                        changed |= NormalizeItem(groupedItem);
                    }
                }

                if (changed)
                {
                    changedProfiles.Add(profile);
                }
            }

            var profilesToUpdate = changedProfiles.Select(p => new
            {
                Id = p.Id,
                Items = p.Items.ToJson()
            });

            var updateSql = "UPDATE \"QualityProfiles\" SET \"Items\" = @Items WHERE \"Id\" = @Id";
            conn.Execute(updateSql, profilesToUpdate, transaction: tran);
        }

        private static bool NormalizeItem(ProfileItem234 item)
        {
            if (item.MaxSize != 0)
            {
                return false;
            }

            item.MaxSize = null;
            return true;
        }

        private static List<Profile234> GetProfiles(IDbConnection conn, IDbTransaction tran)
        {
            var profiles = new List<Profile234>();

            using (var getProfilesCmd = conn.CreateCommand())
            {
                getProfilesCmd.Transaction = tran;
                getProfilesCmd.CommandText = "SELECT \"Id\", \"Items\" FROM \"QualityProfiles\"";

                using (var profileReader = getProfilesCmd.ExecuteReader())
                {
                    while (profileReader.Read())
                    {
                        profiles.Add(new Profile234
                        {
                            Id = profileReader.GetInt32(0),
                            Items = Json.Deserialize<List<ProfileItem234>>(profileReader.GetString(1))
                        });
                    }
                }
            }

            return profiles;
        }
    }

    public class Profile234
    {
        public int Id { get; set; }
        public List<ProfileItem234> Items { get; set; }
    }

    public class ProfileItem234
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Id { get; set; }

        public string Name { get; set; }
        public int? Quality { get; set; }
        public List<ProfileItem234> Items { get; set; }
        public bool Allowed { get; set; }
        public double? MinSize { get; set; }
        public double? MaxSize { get; set; }
        public double? PreferredSize { get; set; }

        public ProfileItem234()
        {
            Items = new List<ProfileItem234>();
        }
    }
}
