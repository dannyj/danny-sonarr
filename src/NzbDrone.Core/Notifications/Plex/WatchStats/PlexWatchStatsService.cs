using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Notifications.Plex.Server;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexWatchStatsService
    {
        List<PlexWatchEvent> GetWatchEvents(PlexServerSettings settings);
        void Test(PlexServerSettings settings);
    }

    public class PlexWatchStatsService : IPlexWatchStatsService
    {
        private readonly IPlexWatchStatsProxy _proxy;

        public PlexWatchStatsService(IPlexWatchStatsProxy proxy)
        {
            _proxy = proxy;
        }

        public List<PlexWatchEvent> GetWatchEvents(PlexServerSettings settings)
        {
            const int pageSize = 100;
            var result = new List<PlexWatchEvent>();
            var start = 0;

            while (true)
            {
                var page = _proxy.GetHistory(settings, start, pageSize);

                if (page.Metadata.Empty())
                {
                    break;
                }

                result.AddRange(page.Metadata.Select(Normalize).Where(x => x != null));

                start += page.Metadata.Count;

                if ((page.TotalSize > 0 && start >= page.TotalSize) || page.Metadata.Count < pageSize)
                {
                    break;
                }
            }

            return result;
        }

        public void Test(PlexServerSettings settings)
        {
            _proxy.GetHistory(settings, 0, 1);
        }

        private PlexWatchEvent Normalize(PlexWatchHistoryItem item)
        {
            if (!string.Equals(item.Type, "episode", StringComparison.OrdinalIgnoreCase) || item.ViewedAt <= 0)
            {
                return null;
            }

            var guids = item.Guid.SelectMany(x => ParseGuid(x.Id)).ToList();
            guids.AddRange(ParseGuid(item.GrandparentGuid));

            return new PlexWatchEvent
            {
                ViewedAtUtc = DateTimeOffset.FromUnixTimeSeconds(item.ViewedAt).UtcDateTime,
                SeriesTitle = item.GrandparentTitle,
                SeriesYear = item.GrandparentYear,
                FilePath = item.Media.SelectMany(x => x.Part).Select(x => x.File).FirstOrDefault(f => f.IsNotNullOrWhiteSpace()),
                User = item.AccountId.HasValue ? new PlexUserRef { Id = item.AccountId.Value.ToString(CultureInfo.InvariantCulture) } : null,
                Guids = guids
            };
        }

        private IEnumerable<PlexMetadataGuid> ParseGuid(string guid)
        {
            if (guid.IsNullOrWhiteSpace())
            {
                yield break;
            }

            var parts = guid.Split(new[] { "://" }, 2, StringSplitOptions.None);

            if (parts.Length != 2 || parts[1].IsNullOrWhiteSpace())
            {
                yield break;
            }

            yield return new PlexMetadataGuid
            {
                Source = parts[0].ToLowerInvariant(),
                Value = parts[1]
            };
        }
    }
}
