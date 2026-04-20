using System.Collections.Generic;
using Dapper;
using NLog;

namespace NzbDrone.Core.Datastore
{
    public interface IPostgresSequenceRepairService
    {
        void RepairIfNeeded(IDatabase database, string databaseName);
    }

    public class PostgresSequenceRepairService : IPostgresSequenceRepairService
    {
        private readonly Logger _logger;

        public PostgresSequenceRepairService(Logger logger)
        {
            _logger = logger;
        }

        public void RepairIfNeeded(IDatabase database, string databaseName)
        {
            if (database.DatabaseType != DatabaseType.PostgreSQL)
            {
                return;
            }

            using var connection = database.OpenConnection();

            var sequences = connection.Query<PostgresSequence>(
                @"SELECT c.table_name AS TableName,
                         c.column_name AS ColumnName,
                         pg_get_serial_sequence(format('%I.%I', c.table_schema, c.table_name), c.column_name) AS SequenceName
                  FROM information_schema.columns c
                  WHERE c.table_schema = 'public' AND c.column_default LIKE 'nextval(%'");

            var repairedSequences = new List<string>();

            foreach (var sequence in sequences)
            {
                if (string.IsNullOrWhiteSpace(sequence.SequenceName))
                {
                    continue;
                }

                var escapedTableName = sequence.TableName.Replace("\"", "\"\"");
                var escapedColumnName = sequence.ColumnName.Replace("\"", "\"\"");
                var targetNextValue = connection.ExecuteScalar<long>(
                    $"SELECT COALESCE(MAX(\"{escapedColumnName}\"), 0) + 1 FROM \"{escapedTableName}\"");

                var state = connection.QuerySingle<SequenceState>($"SELECT last_value AS LastValue, is_called AS IsCalled FROM {sequence.SequenceName}");
                var currentNextValue = state.IsCalled ? state.LastValue + 1 : state.LastValue;

                if (currentNextValue >= targetNextValue)
                {
                    continue;
                }

                connection.Execute("SELECT setval(@sequenceName::regclass, @targetNextValue, false)",
                    new
                    {
                        sequenceName = sequence.SequenceName,
                        targetNextValue
                    });

                repairedSequences.Add($"{sequence.TableName}.{sequence.ColumnName}");
                _logger.Debug("Repaired PostgreSQL sequence {0} for {1}.{2} in the {3} database from next value {4} to {5}",
                    sequence.SequenceName,
                    sequence.TableName,
                    sequence.ColumnName,
                    databaseName,
                    currentNextValue,
                    targetNextValue);
            }

            if (repairedSequences.Count > 0)
            {
                _logger.Warn("Repaired {0} out-of-sync PostgreSQL sequences in the {1} database during startup: {2}",
                    repairedSequences.Count,
                    databaseName,
                    string.Join(", ", repairedSequences));
            }
        }

        public class PostgresSequence
        {
            public string TableName { get; set; }
            public string ColumnName { get; set; }
            public string SequenceName { get; set; }
        }

        public class SequenceState
        {
            public long LastValue { get; set; }
            public bool IsCalled { get; set; }
        }
    }
}
