using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexSeriesMatchService
    {
        Series Match(PlexWatchEvent watchEvent);
    }

    public class PlexSeriesMatchService : IPlexSeriesMatchService
    {
        private readonly ISeriesRepository _seriesRepository;
        private readonly IMediaFileRepository _mediaFileRepository;
        private readonly Logger _logger;

        private Dictionary<string, Series> _pathLookup;

        public PlexSeriesMatchService(ISeriesRepository seriesRepository, IMediaFileRepository mediaFileRepository, Logger logger)
        {
            _seriesRepository = seriesRepository;
            _mediaFileRepository = mediaFileRepository;
            _logger = logger;
        }

        public Series Match(PlexWatchEvent watchEvent)
        {
            var guidMatch = MatchByGuid(watchEvent);

            if (guidMatch != null)
            {
                return guidMatch;
            }

            var pathMatch = MatchByPath(watchEvent.FilePath);

            if (pathMatch != null)
            {
                return pathMatch;
            }

            return MatchByTitle(watchEvent.SeriesTitle, watchEvent.SeriesYear);
        }

        private Series MatchByGuid(PlexWatchEvent watchEvent)
        {
            foreach (var guid in watchEvent.Guids)
            {
                Series match = null;

                switch (guid.Source)
                {
                    case "tvdb":
                        if (int.TryParse(guid.Value, out var tvdbId))
                        {
                            match = _seriesRepository.FindByTvdbId(tvdbId);
                        }

                        break;
                    case "tmdb":
                        if (int.TryParse(guid.Value, out var tmdbId))
                        {
                            match = _seriesRepository.All().SingleOrDefault(x => x.TmdbId == tmdbId);
                        }

                        break;
                    case "imdb":
                        match = _seriesRepository.FindByImdbId(Parser.Parser.NormalizeImdbId(guid.Value));
                        break;
                }

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private Series MatchByPath(string filePath)
        {
            if (filePath.IsNullOrWhiteSpace())
            {
                return null;
            }

            _pathLookup ??= BuildPathLookup();

            _pathLookup.TryGetValue(NormalizePath(filePath), out var match);

            return match;
        }

        private Series MatchByTitle(string title, int? year)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return null;
            }

            var cleanTitle = title.CleanSeriesTitle();
            var matches = _seriesRepository.All().Where(x => x.CleanTitle == cleanTitle).ToList();

            if (year.HasValue)
            {
                matches = matches.Where(x => x.Year == year.Value).ToList();
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                _logger.Warn("Ambiguous Plex title/year match for {0} ({1}), skipping", title, year);
            }

            return null;
        }

        private Dictionary<string, Series> BuildPathLookup()
        {
            var seriesById = _seriesRepository.All().ToDictionary(x => x.Id);
            var result = new Dictionary<string, Series>(StringComparer.OrdinalIgnoreCase);

            foreach (var mediaFile in _mediaFileRepository.All())
            {
                if (!seriesById.TryGetValue(mediaFile.SeriesId, out var series) ||
                    series.Path.IsNullOrWhiteSpace() ||
                    mediaFile.RelativePath.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var fullPath = NormalizePath(Path.Combine(series.Path, mediaFile.RelativePath));

                if (!result.ContainsKey(fullPath))
                {
                    result[fullPath] = series;
                }
            }

            return result;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar)
                       .Replace('/', Path.DirectorySeparatorChar)
                       .Trim();
        }
    }
}
