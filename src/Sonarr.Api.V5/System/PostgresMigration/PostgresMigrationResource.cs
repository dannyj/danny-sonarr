using NzbDrone.Core.Datastore.PostgresMigration;

namespace Sonarr.Api.V5.System.PostgresMigration;

public class PostgresMigrationConnectionResource
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string MainDb { get; set; } = string.Empty;
    public string LogDb { get; set; } = string.Empty;

    public PostgresMigrationConnectionInfo ToModel()
    {
        return new PostgresMigrationConnectionInfo
        {
            Host = Host,
            Port = Port,
            User = User,
            Password = Password,
            MainDb = MainDb,
            LogDb = LogDb
        };
    }
}

public class PostgresMigrationStatusResource
{
    public string State { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string CurrentDatabaseType { get; set; } = string.Empty;
    public bool RestartPending { get; set; }
    public bool RestartRequired { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class PostgresMigrationValidationResource
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}
