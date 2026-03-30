using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Notifications.Plex.Server;

namespace NzbDrone.Core.Notifications.Plex.WatchStats
{
    public interface IPlexWatchStatsProxy
    {
        PlexWatchHistoryPage GetHistory(PlexServerSettings settings, int start, int pageSize);
    }

    public class PlexWatchStatsProxy : IPlexWatchStatsProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public PlexWatchStatsProxy(IHttpClient httpClient, IConfigService configService, Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public PlexWatchHistoryPage GetHistory(PlexServerSettings settings, int start, int pageSize)
        {
            var request = BuildRequest("status/sessions/history/all", HttpMethod.Get, settings)
                .AddQueryParam("sort", "viewedAt:desc")
                .AddQueryParam("X-Plex-Container-Start", start)
                .AddQueryParam("X-Plex-Container-Size", pageSize);

            var response = ProcessRequest(request);
            var result = Json.Deserialize<PlexResponse<PlexWatchHistoryContainer>>(response);

            return new PlexWatchHistoryPage
            {
                TotalSize = result?.MediaContainer?.TotalSize ?? 0,
                Metadata = result?.MediaContainer?.Metadata ?? new List<PlexWatchHistoryItem>()
            };
        }

        private HttpRequestBuilder BuildRequest(string resource, HttpMethod method, PlexServerSettings settings)
        {
            var scheme = settings.UseSsl ? "https" : "http";

            var requestBuilder = new HttpRequestBuilder($"{scheme}://{settings.Host.ToUrlHost()}:{settings.Port}{settings.UrlBase}")
                .Accept(HttpAccept.Json)
                .AddQueryParam("X-Plex-Client-Identifier", _configService.PlexClientIdentifier)
                .AddQueryParam("X-Plex-Product", BuildInfo.AppName)
                .AddQueryParam("X-Plex-Platform", "Windows")
                .AddQueryParam("X-Plex-Platform-Version", "7")
                .AddQueryParam("X-Plex-Device-Name", BuildInfo.AppName)
                .AddQueryParam("X-Plex-Version", BuildInfo.Version.ToString());

            if (settings.AuthToken.IsNotNullOrWhiteSpace())
            {
                requestBuilder.AddQueryParam("X-Plex-Token", settings.AuthToken);
            }

            requestBuilder.ResourceUrl = resource;
            requestBuilder.Method = method;

            return requestBuilder;
        }

        private string ProcessRequest(HttpRequestBuilder requestBuilder)
        {
            var httpRequest = requestBuilder.Build();

            _logger.Debug("Plex watch stats Url: {0}", httpRequest.Url);

            try
            {
                return _httpClient.Execute(httpRequest).Content;
            }
            catch (HttpException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new PlexAuthenticationException("Unauthorized - AuthToken is invalid");
                }

                throw new PlexException("Unable to connect to Plex Media Server. Status Code: {0}", ex.Response.StatusCode);
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.TrustFailure)
                {
                    throw new PlexException("Unable to connect to Plex Media Server, certificate validation failed.", ex);
                }

                throw new PlexException($"Unable to connect to Plex Media Server, {ex.Message}", ex);
            }
        }
    }

    public class PlexWatchHistoryPage
    {
        public int TotalSize { get; set; }
        public List<PlexWatchHistoryItem> Metadata { get; set; }
    }

    public class PlexWatchHistoryContainer
    {
        public int TotalSize { get; set; }
        public List<PlexWatchHistoryItem> Metadata { get; set; } = new();
    }

    public class PlexWatchHistoryItem
    {
        public string RatingKey { get; set; }
        public string Type { get; set; }
        public long ViewedAt { get; set; }
        public string GrandparentTitle { get; set; }
        public int? GrandparentYear { get; set; }
        public string GrandparentGuid { get; set; }
        public string Title { get; set; }
        public int? ParentIndex { get; set; }
        public int? Index { get; set; }
        public int? AccountId { get; set; }
        public List<PlexWatchHistoryGuid> Guid { get; set; } = new();
        public List<PlexWatchHistoryMedia> Media { get; set; } = new();
    }

    public class PlexWatchHistoryGuid
    {
        public string Id { get; set; }
    }

    public class PlexWatchHistoryMedia
    {
        public List<PlexWatchHistoryPart> Part { get; set; } = new();
    }

    public class PlexWatchHistoryPart
    {
        public string File { get; set; }
    }
}
