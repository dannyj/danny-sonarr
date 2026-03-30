using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Download
{
    public interface ISeriesDownloadCleanupService
    {
        void RemoveTrackedDownloads(List<Series> series);
    }

    public class SeriesDownloadCleanupService : ISeriesDownloadCleanupService
    {
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly Logger _logger;

        public SeriesDownloadCleanupService(ITrackedDownloadService trackedDownloadService,
                                            IProvideDownloadClient downloadClientProvider,
                                            Logger logger)
        {
            _trackedDownloadService = trackedDownloadService;
            _downloadClientProvider = downloadClientProvider;
            _logger = logger;
        }

        public void RemoveTrackedDownloads(List<Series> series)
        {
            var seriesIds = series.Select(s => s.Id).ToHashSet();
            var tvdbIds = series.Select(s => s.TvdbId).ToHashSet();

            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                .Where(t =>
                    t.RemoteEpisode?.Series != null &&
                    (seriesIds.Contains(t.RemoteEpisode.Series.Id) || tvdbIds.Contains(t.RemoteEpisode.Series.TvdbId)))
                .ToList();

            var removedDownloadIds = new List<string>();

            foreach (var trackedDownload in trackedDownloads)
            {
                try
                {
                    var downloadClient = _downloadClientProvider.Get(trackedDownload.DownloadClient);

                    downloadClient.RemoveItem(trackedDownload.DownloadItem, true);
                    trackedDownload.DownloadItem.Removed = true;
                    removedDownloadIds.Add(trackedDownload.DownloadItem.DownloadId);
                }
                catch (System.Exception e)
                {
                    _logger.Error(e, "Couldn't remove item {0} from client while deleting series", trackedDownload.DownloadItem.Title);
                }
            }

            if (removedDownloadIds.Any())
            {
                _trackedDownloadService.StopTracking(removedDownloadIds);
            }
        }
    }
}
