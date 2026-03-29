using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MediaInfo.MediaInfoFormatterTests
{
    [TestFixture]
    public class FormatAudioChannelsFixture : TestBase
    {
        [Test]
        public void should_return_zero_when_audio_stream_is_null()
        {
            MediaInfoFormatter.FormatAudioChannels(null).Should().Be(0.0m);
        }
    }
}
