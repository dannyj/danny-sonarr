using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Datastore.PostgresMigration
{
    public enum PostgresMigrationState
    {
        Idle,
        PendingRestart,
        Migrating,
        RestartRequired,
        Succeeded,
        Failed
    }

    public class PostgresMigrationConnectionInfo
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string MainDb { get; set; }
        public string LogDb { get; set; }
    }

    public class PostgresMigrationJob
    {
        public PostgresMigrationState State { get; set; }
        public string Step { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool RestartRequired { get; set; }
        public bool RestartPending { get; set; }
        public PostgresMigrationConnectionInfo ConnectionInfo { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class PostgresMigrationValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
