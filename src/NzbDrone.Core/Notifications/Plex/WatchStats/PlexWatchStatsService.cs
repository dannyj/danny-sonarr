using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Notifications.Plex.Server;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexWatchStatsService
    {
        PlexWatchEventFetchResult GetWatchEvents(PlexServerSettings settings, DateTime? lastSuccessfulViewedAtUtc, TimeSpan overlap);
        void Test(PlexServerSettings settings);
    }

    public class PlexWatchStatsService : IPlexWatchStatsService
    {
        private readonly IPlexWatchStatsProxy _proxy;

        public PlexWatchStatsService(IPlexWatchStatsProxy proxy)
        {
            _proxy = proxy;
        }

        public PlexWatchEventFetchResult GetWatchEvents(PlexServerSettings settings, DateTime? lastSuccessfulViewedAtUtc, TimeSpan overlap)
        {
            const int pageSize = 100;
            var threshold = lastSuccessfulViewedAtUtc.HasValue ? lastSuccessfulViewedAtUtc.Value.Subtract(overlap) : (DateTime?)null;
            var result = new List<PlexWatchEvent>();
            var start = 0;
            var rawEventsFetched = 0;
            var olderEventsSkipped = 0;
            var pagesFetched = 0;

            while (true)
            {
                var page = _proxy.GetHistory(settings, start, pageSize);
                pagesFetched++;

                if (page.Metadata.Empty())
                {
                    break;
                }

                rawEventsFetched += page.Metadata.Count;

                var normalizedPage = page.Metadata.Select(Normalize).Where(x => x != null).ToList();

                if (threshold.HasValue)
                {
                    olderEventsSkipped += normalizedPage.Count(x => x.ViewedAtUtc < threshold.Value);
                    normalizedPage = normalizedPage.Where(x => x.ViewedAtUtc >= threshold.Value).ToList();
                }

                result.AddRange(normalizedPage);

                start += page.Metadata.Count;

                var oldestViewedAtUtc = page.Metadata
                    .Where(x => x.ViewedAt > 0)
                    .Select(x => DateTimeOffset.FromUnixTimeSeconds(x.ViewedAt).UtcDateTime)
                    .DefaultIfEmpty(DateTime.MaxValue)
                    .Min();

                if ((page.TotalSize > 0 && start >= page.TotalSize) ||
                    page.Metadata.Count < pageSize ||
                    (threshold.HasValue && oldestViewedAtUtc < threshold.Value))
                {
                    break;
                }
            }

            return new PlexWatchEventFetchResult
            {
                Events = result,
                PagesFetched = pagesFetched,
                RawEventsFetched = rawEventsFetched,
                OlderEventsSkipped = olderEventsSkipped
            };
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
                EventKey = BuildEventKey(item),
                ViewedAtUtc = DateTimeOffset.FromUnixTimeSeconds(item.ViewedAt).UtcDateTime,
                SeriesTitle = item.GrandparentTitle,
                SeriesYear = item.GrandparentYear,
                EpisodeTitle = item.Title,
                SeasonNumber = item.ParentIndex,
                EpisodeNumber = item.Index,
                RatingKey = item.RatingKey,
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

        private static string BuildEventKey(PlexWatchHistoryItem item)
        {
            var fingerprint = string.Join("|", new[]
            {
                item.RatingKey ?? string.Empty,
                item.ViewedAt.ToString(CultureInfo.InvariantCulture),
                item.AccountId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.GrandparentTitle ?? string.Empty,
                item.GrandparentYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.ParentIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.Index?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.Media.SelectMany(x => x.Part).Select(x => x.File).FirstOrDefault(f => f.IsNotNullOrWhiteSpace()) ?? string.Empty
            });

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));
                return Convert.ToHexString(hash);
            }
        }
    }

    public class PlexWatchEventFetchResult
    {
        public List<PlexWatchEvent> Events { get; set; } = new();
        public int PagesFetched { get; set; }
        public int RawEventsFetched { get; set; }
        public int OlderEventsSkipped { get; set; }
    }
}
