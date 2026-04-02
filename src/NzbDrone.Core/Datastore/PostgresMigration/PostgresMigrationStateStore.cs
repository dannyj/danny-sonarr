using System;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Datastore.PostgresMigration
{
    public interface IPostgresMigrationStateStore
    {
        PostgresMigrationJob Get();
        void Save(PostgresMigrationJob job);
        void Clear();
    }

    public class PostgresMigrationStateStore : IPostgresMigrationStateStore
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly Logger _logger;

        public PostgresMigrationStateStore(IDiskProvider diskProvider,
                                           IAppFolderInfo appFolderInfo,
                                           Logger logger)
        {
            _diskProvider = diskProvider;
            _appFolderInfo = appFolderInfo;
            _logger = logger;
        }

        public PostgresMigrationJob Get()
        {
            var path = _appFolderInfo.GetPostgresMigrationState();

            if (!_diskProvider.FileExists(path))
            {
                return new PostgresMigrationJob
                {
                    State = PostgresMigrationState.Idle,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            try
            {
                return STJson.Deserialize<PostgresMigrationJob>(_diskProvider.ReadAllText(path));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to read Postgres migration state");

                return new PostgresMigrationJob
                {
                    State = PostgresMigrationState.Failed,
                    UpdatedAt = DateTime.UtcNow,
                    Error = "Postgres migration state file is unreadable"
                };
            }
        }

        public void Save(PostgresMigrationJob job)
        {
            job.UpdatedAt = DateTime.UtcNow;

            var path = _appFolderInfo.GetPostgresMigrationState();
            _diskProvider.WriteAllText(path, STJson.ToJson(job));
        }

        public void Clear()
        {
            var path = _appFolderInfo.GetPostgresMigrationState();

            if (_diskProvider.FileExists(path))
            {
                _diskProvider.DeleteFile(path);
            }
        }
    }
}
