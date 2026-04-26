using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using Dapper;
using NLog;
using Npgsql;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Datastore.PostgresMigration
{
    public interface IPostgresMigrationService
    {
        PostgresMigrationJob GetStatus();
        PostgresMigrationValidationResult Validate(PostgresMigrationConnectionInfo connectionInfo);
        PostgresMigrationJob Start(PostgresMigrationConnectionInfo connectionInfo);
    }

    public class PostgresMigrationService : IPostgresMigrationService, IHandle<ApplicationStartedEvent>
    {
        private readonly IPostgresMigrationStateStore _stateStore;
        private readonly IMainDatabase _mainDatabase;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IMigrationController _migrationController;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly ILifecycleService _lifecycleService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public PostgresMigrationService(IPostgresMigrationStateStore stateStore,
                                        IMainDatabase mainDatabase,
                                        IConfigFileProvider configFileProvider,
                                        IMigrationController migrationController,
                                        IAppFolderInfo appFolderInfo,
                                        ILifecycleService lifecycleService,
                                        IDiskProvider diskProvider,
                                        Logger logger)
        {
            _stateStore = stateStore;
            _mainDatabase = mainDatabase;
            _configFileProvider = configFileProvider;
            _migrationController = migrationController;
            _appFolderInfo = appFolderInfo;
            _lifecycleService = lifecycleService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public PostgresMigrationJob GetStatus()
        {
            var status = _stateStore.Get();

            if (status.ConnectionInfo != null)
            {
                status.ConnectionInfo.Password = string.Empty;
            }

            return status;
        }

        public PostgresMigrationValidationResult Validate(PostgresMigrationConnectionInfo connectionInfo)
        {
            ValidateRequest(connectionInfo);

            if (_mainDatabase.DatabaseType != DatabaseType.SQLite)
            {
                return new PostgresMigrationValidationResult
                {
                    IsValid = false,
                    Message = "Postgres migration is only available when Sonarr is currently using SQLite."
                };
            }

            var warnings = new List<string>();

            using var adminConnection = new NpgsqlConnection(BuildConnectionString(connectionInfo, "postgres"));
            adminConnection.Open();

            var canCreateDatabase = adminConnection.ExecuteScalar<bool>("SELECT rolcreatedb FROM pg_roles WHERE rolname = current_user");

            ValidateDatabase(adminConnection, connectionInfo, connectionInfo.MainDb, canCreateDatabase, warnings);
            ValidateDatabase(adminConnection, connectionInfo, connectionInfo.LogDb, canCreateDatabase, warnings);

            return new PostgresMigrationValidationResult
            {
                IsValid = true,
                Message = "Connection validated successfully.",
                Warnings = warnings
            };
        }

        public PostgresMigrationJob Start(PostgresMigrationConnectionInfo connectionInfo)
        {
            var validation = Validate(connectionInfo);

            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Message);
            }

            var status = new PostgresMigrationJob
            {
                State = PostgresMigrationState.PendingRestart,
                Step = "queued",
                Message = "Migration has been scheduled. Sonarr will restart to begin the copy.",
                RestartPending = true,
                RestartRequired = true,
                ConnectionInfo = new PostgresMigrationConnectionInfo
                {
                    Host = connectionInfo.Host,
                    Port = connectionInfo.Port,
                    User = connectionInfo.User,
                    Password = connectionInfo.Password,
                    MainDb = connectionInfo.MainDb,
                    LogDb = connectionInfo.LogDb
                },
                Warnings = validation.Warnings
            };

            _stateStore.Save(status);
            _lifecycleService.Restart();

            return GetStatus();
        }

        [EventHandleOrder(EventHandleOrder.First)]
        public void Handle(ApplicationStartedEvent message)
        {
            var state = _stateStore.Get();

            if (state.State == PostgresMigrationState.RestartRequired &&
                _mainDatabase.DatabaseType == DatabaseType.PostgreSQL)
            {
                state.State = PostgresMigrationState.Succeeded;
                state.Step = "complete";
                state.Message = "SQLite data was migrated to Postgres successfully.";
                state.Error = null;
                state.RestartPending = false;
                state.RestartRequired = false;
                state.CompletedAt = DateTime.UtcNow;
                state.ConnectionInfo = null;
                _stateStore.Save(state);
                return;
            }

            if (state.State != PostgresMigrationState.PendingRestart || state.ConnectionInfo == null)
            {
                return;
            }

            state.State = PostgresMigrationState.Migrating;
            state.Step = "starting";
            state.Message = "Preparing SQLite snapshots.";
            state.Error = null;
            state.StartedAt = DateTime.UtcNow;
            state.RestartPending = false;
            _stateStore.Save(state);

            try
            {
                RunMigration(state);

                state.State = PostgresMigrationState.RestartRequired;
                state.Step = "restart";
                state.Message = "Migration finished. Restarting onto Postgres.";
                state.Error = null;
                state.RestartPending = true;
                state.RestartRequired = true;
                state.ConnectionInfo = null;
                _stateStore.Save(state);

                _lifecycleService.Restart();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Postgres migration failed");

                state.State = PostgresMigrationState.Failed;
                state.Step = "failed";
                state.Message = "Migration failed. Sonarr is still running on SQLite.";
                state.Error = ex.Message;
                state.RestartPending = false;
                state.RestartRequired = false;
                state.CompletedAt = DateTime.UtcNow;
                state.ConnectionInfo = null;
                _stateStore.Save(state);
            }
        }

        private void RunMigration(PostgresMigrationJob state)
        {
            var mainSnapshot = Path.Combine(_appFolderInfo.TempFolder, "sonarr-main-postgres-migration.db");
            var logSnapshot = Path.Combine(_appFolderInfo.TempFolder, "sonarr-log-postgres-migration.db");

            CreateSqliteSnapshot(_appFolderInfo.GetDatabase(), mainSnapshot);

            if (_configFileProvider.LogDbEnabled)
            {
                CreateSqliteSnapshot(_appFolderInfo.GetLogDatabase(), logSnapshot);
            }

            state.Step = "validating";
            state.Message = "Validating target Postgres connection.";
            _stateStore.Save(state);
            _ = Validate(state.ConnectionInfo);

            state.Step = "main";
            state.Message = "Migrating main database to Postgres.";
            _stateStore.Save(state);
            MigrateSqliteDatabase(mainSnapshot, state.ConnectionInfo, MigrationType.Main, state.ConnectionInfo.MainDb);

            if (_configFileProvider.LogDbEnabled)
            {
                state.Step = "log";
                state.Message = "Migrating log database to Postgres.";
                _stateStore.Save(state);
                MigrateSqliteDatabase(logSnapshot, state.ConnectionInfo, MigrationType.Log, state.ConnectionInfo.LogDb);
            }

            state.Step = "config";
            state.Message = "Writing Postgres settings to config.xml.";
            _stateStore.Save(state);
            SavePostgresConfig(state.ConnectionInfo);
        }

        private void ValidateDatabase(NpgsqlConnection adminConnection,
                                      PostgresMigrationConnectionInfo connectionInfo,
                                      string databaseName,
                                      bool canCreateDatabase,
                                      List<string> warnings)
        {
            var exists = adminConnection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM pg_database WHERE datname = @name",
                new { name = databaseName }) > 0;

            if (!exists && !canCreateDatabase)
            {
                throw new InvalidOperationException($"Database '{databaseName}' does not exist and the current PostgreSQL role cannot create databases.");
            }

            if (exists)
            {
                using var connection = new NpgsqlConnection(BuildConnectionString(connectionInfo, databaseName));
                connection.Open();

                var tableCount = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'");

                if (tableCount > 0)
                {
                    warnings.Add($"Database '{databaseName}' already contains tables and will be overwritten during migration.");
                }
            }
        }

        private void MigrateSqliteDatabase(string sqlitePath,
                                           PostgresMigrationConnectionInfo connectionInfo,
                                           MigrationType migrationType,
                                           string databaseName)
        {
            EnsureDatabaseExists(connectionInfo, databaseName);

            var postgresConnectionString = BuildConnectionString(connectionInfo, databaseName);
            _migrationController.Migrate(postgresConnectionString, new MigrationContext(migrationType), DatabaseType.PostgreSQL);

            using var source = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = sqlitePath,
                ReadOnly = true
            }.ConnectionString);
            using var target = new NpgsqlConnection(postgresConnectionString);

            source.Open();
            target.Open();

            ClearPostgresTables(target);
            CopySqliteTables(source, target);
            RepairSequences(target);
        }

        private void EnsureDatabaseExists(PostgresMigrationConnectionInfo connectionInfo, string databaseName)
        {
            using var adminConnection = new NpgsqlConnection(BuildConnectionString(connectionInfo, "postgres"));
            adminConnection.Open();

            var exists = adminConnection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM pg_database WHERE datname = @name",
                new { name = databaseName }) > 0;

            if (exists)
            {
                return;
            }

            adminConnection.Execute($"CREATE DATABASE {QuotePostgresLiteralIdentifier(databaseName)}");
        }

        private void ClearPostgresTables(NpgsqlConnection connection)
        {
            var tables = connection.Query<string>(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> 'VersionInfo'");

            var tableList = tables.Select(t => $"\"{t.Replace("\"", "\"\"")}\"").ToList();

            if (!tableList.Any())
            {
                return;
            }

            connection.Execute($"TRUNCATE TABLE {string.Join(", ", tableList)} RESTART IDENTITY CASCADE");
        }

        private void CopySqliteTables(SQLiteConnection source, NpgsqlConnection target)
        {
            var sourceTables = source.Query<string>(
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name <> 'VersionInfo'");

            var targetTables = target.Query<string>(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'")
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            foreach (var table in sourceTables)
            {
                if (!targetTables.Contains(table))
                {
                    continue;
                }

                CopySqliteTable(source, target, table);
            }
        }

        private void CopySqliteTable(SQLiteConnection source, NpgsqlConnection target, string table)
        {
            var targetColumns = target.Query<(string ColumnName, string DataType, string IsIdentity, string IdentityGeneration)>(
                @"SELECT column_name AS ColumnName,
                         data_type AS DataType,
                         is_identity AS IsIdentity,
                         identity_generation AS IdentityGeneration
                  FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = @table
                  ORDER BY ordinal_position",
                new { table })
                .ToList();

            var targetColumnTypes = targetColumns
                .ToDictionary(x => x.ColumnName, x => x.DataType, StringComparer.InvariantCultureIgnoreCase);

            if (!targetColumnTypes.Any())
            {
                return;
            }

            var hasAlwaysIdentityColumn = targetColumns.Any(x =>
                x.IsIdentity.Equals("YES", StringComparison.InvariantCultureIgnoreCase) &&
                x.IdentityGeneration.Equals("ALWAYS", StringComparison.InvariantCultureIgnoreCase));

            using var sourceCommand = source.CreateCommand();
            sourceCommand.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\"";

            using var reader = sourceCommand.ExecuteReader();

            if (!reader.HasRows)
            {
                return;
            }

            while (reader.Read())
            {
                var columnNames = new List<string>();
                var parameterNames = new List<string>();
                var parameters = new DynamicParameters();

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);

                    if (!targetColumnTypes.TryGetValue(columnName, out var dataType))
                    {
                        continue;
                    }

                    var parameterName = $"p{i}";
                    columnNames.Add($"\"{columnName.Replace("\"", "\"\"")}\"");
                    parameterNames.Add($"@{parameterName}");
                    parameters.Add(parameterName, ConvertValue(reader.IsDBNull(i) ? null : reader.GetValue(i), dataType));
                }

                if (!columnNames.Any())
                {
                    continue;
                }

                var overrideClause = hasAlwaysIdentityColumn ? " OVERRIDING SYSTEM VALUE" : string.Empty;
                var sql = $"INSERT INTO \"{table.Replace("\"", "\"\"")}\" ({string.Join(", ", columnNames)}){overrideClause} VALUES ({string.Join(", ", parameterNames)})";
                target.Execute(sql, parameters);
            }
        }

        private void RepairSequences(NpgsqlConnection connection)
        {
            var sequences = connection.Query<(string TableName, string ColumnName, string SequenceName)>(
                @"SELECT c.table_name AS TableName,
                         c.column_name AS ColumnName,
                         pg_get_serial_sequence(format('%I.%I', c.table_schema, c.table_name), c.column_name) AS SequenceName
                  FROM information_schema.columns c
                  WHERE c.table_schema = 'public'
                    AND (c.column_default LIKE 'nextval(%' OR c.is_identity = 'YES')");

            foreach (var sequence in sequences.Where(x => x.SequenceName.IsNotNullOrWhiteSpace()))
            {
                var tableName = sequence.TableName.Replace("\"", "\"\"");
                var columnName = sequence.ColumnName.Replace("\"", "\"\"");

                var sql = $"SELECT setval(@sequenceName::regclass, COALESCE((SELECT MAX(\"{columnName}\") FROM \"{tableName}\"), 0) + 1, false)";
                connection.Execute(sql, new { sequenceName = sequence.SequenceName });
            }
        }

        private void SavePostgresConfig(PostgresMigrationConnectionInfo connectionInfo)
        {
            _configFileProvider.SaveConfigDictionary(new Dictionary<string, object>
            {
                { nameof(IConfigFileProvider.PostgresHost), connectionInfo.Host },
                { nameof(IConfigFileProvider.PostgresPort), connectionInfo.Port },
                { nameof(IConfigFileProvider.PostgresUser), connectionInfo.User },
                { nameof(IConfigFileProvider.PostgresPassword), connectionInfo.Password },
                { nameof(IConfigFileProvider.PostgresMainDb), connectionInfo.MainDb },
                { nameof(IConfigFileProvider.PostgresLogDb), connectionInfo.LogDb },
                { nameof(IConfigFileProvider.PostgresMainDbConnectionString), string.Empty },
                { nameof(IConfigFileProvider.PostgresLogDbConnectionString), string.Empty }
            });
        }

        private void CreateSqliteSnapshot(string sourcePath, string destinationPath)
        {
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal", "-journal" })
            {
                if (_diskProvider.FileExists(destinationPath + suffix))
                {
                    _diskProvider.DeleteFile(destinationPath + suffix);
                }
            }

            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = sourcePath,
                ReadOnly = false
            };

            var snapshotBuilder = new SQLiteConnectionStringBuilder
            {
                DataSource = destinationPath
            };

            using var sourceConnection = new SQLiteConnection(builder.ConnectionString);
            using var snapshotConnection = new SQLiteConnection(snapshotBuilder.ConnectionString);

            sourceConnection.Open();
            snapshotConnection.Open();
            sourceConnection.BackupDatabase(snapshotConnection, "main", "main", -1, null, 500);
        }

        private void ValidateRequest(PostgresMigrationConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
            {
                throw new InvalidOperationException("Postgres connection details were not provided.");
            }

            if (connectionInfo.Host.IsNullOrWhiteSpace() ||
                connectionInfo.User.IsNullOrWhiteSpace() ||
                connectionInfo.Password.IsNullOrWhiteSpace() ||
                connectionInfo.MainDb.IsNullOrWhiteSpace() ||
                connectionInfo.LogDb.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Host, user, password, main database, and log database are required.");
            }

            if (connectionInfo.Port <= 0)
            {
                throw new InvalidOperationException("A valid Postgres port is required.");
            }

            if (connectionInfo.MainDb.Equals(connectionInfo.LogDb, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException("Main and log PostgreSQL databases must be different.");
            }
        }

        private static object ConvertValue(object value, string dataType)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            switch (dataType)
            {
                case "boolean":
                    if (value is bool boolean)
                    {
                        return boolean;
                    }

                    if (value is long longValue)
                    {
                        return longValue != 0;
                    }

                    if (value is int intValue)
                    {
                        return intValue != 0;
                    }

                    if (value is string boolString)
                    {
                        return boolString == "1" || bool.Parse(boolString);
                    }

                    break;
                case "integer":
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                case "smallint":
                    return Convert.ToInt16(value, CultureInfo.InvariantCulture);
                case "bigint":
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case "numeric":
                    return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                case "real":
                    return Convert.ToSingle(value, CultureInfo.InvariantCulture);
                case "double precision":
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                case "uuid":
                    if (value is Guid guid)
                    {
                        return guid;
                    }

                    return Guid.Parse(value.ToString());
                case "timestamp without time zone":
                case "timestamp with time zone":
                    if (value is DateTime dateTime)
                    {
                        return dateTime;
                    }

                    return DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            return value;
        }

        private static string BuildConnectionString(PostgresMigrationConnectionInfo connectionInfo, string databaseName)
        {
            return new NpgsqlConnectionStringBuilder
            {
                Host = connectionInfo.Host,
                Port = connectionInfo.Port,
                Username = connectionInfo.User,
                Password = connectionInfo.Password,
                Database = databaseName,
                Enlist = false
            }.ConnectionString;
        }

        private static string QuotePostgresLiteralIdentifier(string identifier)
        {
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }
    }
}
