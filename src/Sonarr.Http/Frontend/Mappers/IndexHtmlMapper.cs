using System;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Analytics;
using NzbDrone.Core.Configuration;
using StackExchange.Profiling;

namespace Sonarr.Http.Frontend.Mappers
{
    public class IndexHtmlMapper : HtmlMapperBase
    {
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IAnalyticsService _analyticsService;

        public IndexHtmlMapper(IAppFolderInfo appFolderInfo,
                               IDiskProvider diskProvider,
                               IConfigFileProvider configFileProvider,
                               IAnalyticsService analyticsService,
                               Lazy<ICacheBreakerProvider> cacheBreakProviderFactory,
                               Logger logger)
            : base(diskProvider, configFileProvider, cacheBreakProviderFactory, logger)
        {
            _appFolderInfo = appFolderInfo;
            _configFileProvider = configFileProvider;
            _analyticsService = analyticsService;
        }

        protected override string FolderPath => Path.Combine(_appFolderInfo.StartUpFolder, _configFileProvider.UiFolder);
        protected override string HtmlPath => Path.Combine(FolderPath, "index.html");

        protected override string MapPath(string resourceUrl)
        {
            return HtmlPath;
        }

        public override bool CanHandle(string resourceUrl)
        {
            resourceUrl = resourceUrl.ToLowerInvariant();

            return !resourceUrl.StartsWith("/content") &&
                   !resourceUrl.StartsWith("/mediacover") &&
                   !resourceUrl.Contains('.') &&
                   !resourceUrl.StartsWith("/login");
        }

        protected override string GetHtmlText(HttpContext context)
        {
            var html = base.GetHtmlText(context);
            var theme = _configFileProvider.Theme;
            var sonarrState = JsonSerializer.Serialize(new
            {
                apiRoot = $"{_configFileProvider.UrlBase}/api/v3",
                apiKey = _configFileProvider.ApiKey,
                release = BuildInfo.Release,
                version = BuildInfo.Version.ToString(),
                instanceName = _configFileProvider.InstanceName,
                theme,
                branch = _configFileProvider.Branch.ToLower(),
                analytics = _analyticsService.IsEnabled,
                userHash = HashUtil.AnonymousToken(),
                urlBase = _configFileProvider.UrlBase,
                isProduction = RuntimeInfo.IsProduction
            });

            html = html.Replace("_THEME_", theme);
            html = html.Replace("__SONARR_STATE__", sonarrState);

            if (_configFileProvider.ProfilerEnabled)
            {
                var includes = MiniProfiler.Current?.RenderIncludes(context);

                if (includes == null || includes.Value.IsNullOrWhiteSpace())
                {
                    html = html.Replace("__MINI_PROFILER__", "");
                }
                else
                {
                    html = html.Replace("__MINI_PROFILER__", includes.Value);
                }
            }
            else
            {
                html = html.Replace("__MINI_PROFILER__", "");
            }

            return html;
        }
    }
}
