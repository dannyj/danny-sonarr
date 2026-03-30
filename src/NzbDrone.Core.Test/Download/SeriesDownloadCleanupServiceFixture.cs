using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class SeriesDownloadCleanupServiceFixture : CoreTest<SeriesDownloadCleanupService>
    {
        [Test]
        public void should_remove_tracked_downloads_for_deleted_series()
        {
            var deletedSeries = Builder<Series>.CreateNew()
                .With(s => s.Id = 1)
                .With(s => s.TvdbId = 100)
                .Build();

            var otherSeries = Builder<Series>.CreateNew()
                .With(s => s.Id = 2)
                .With(s => s.TvdbId = 200)
                .Build();

            var matchingTrackedDownload = new TrackedDownload
            {
                DownloadClient = 10,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "matching-download",
                    Title = "Matching Series",
                    DownloadClientInfo = new DownloadClientItemClientInfo()
                },
                RemoteEpisode = new RemoteEpisode
                {
                    Series = deletedSeries
                }
            };

            var otherTrackedDownload = new TrackedDownload
            {
                DownloadClient = 11,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = "other-download",
                    Title = "Other Series",
                    DownloadClientInfo = new DownloadClientItemClientInfo()
                },
                RemoteEpisode = new RemoteEpisode
                {
                    Series = otherSeries
                }
            };

            var downloadClient = Mocker.GetMock<IDownloadClient>();
            Mocker.GetMock<IProvideDownloadClient>()
                .Setup(s => s.Get(10))
                .Returns(downloadClient.Object);

            Mocker.GetMock<ITrackedDownloadService>()
                .Setup(s => s.GetTrackedDownloads())
                .Returns(new List<TrackedDownload> { matchingTrackedDownload, otherTrackedDownload });

            Subject.RemoveTrackedDownloads(new List<Series> { deletedSeries });

            downloadClient.Verify(v => v.RemoveItem(matchingTrackedDownload.DownloadItem, true), Times.Once());
            Mocker.GetMock<IProvideDownloadClient>()
                .Verify(v => v.Get(11), Times.Never());
            Mocker.GetMock<ITrackedDownloadService>()
                .Verify(v => v.StopTracking(It.Is<List<string>>(ids => ids.Count == 1 && ids[0] == "matching-download")), Times.Once());
        }
    }
}
