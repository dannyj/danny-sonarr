using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    public class PostgresSequenceRepairServiceFixture : DbTest
    {
        [Test]
        public void should_repair_a_postgres_sequence_that_fell_behind_table_data()
        {
            if (Db.DatabaseType != DatabaseType.PostgreSQL)
            {
                return;
            }

            using (var connection = Mocker.Resolve<IMainDatabase>().OpenConnection())
            {
                connection.Execute("INSERT INTO \"Tags\" (\"Id\", \"Label\") VALUES (100, 'sequence-repair-test')");
                connection.Execute("SELECT setval('public.\"Tags_Id_seq\"'::regclass, 1, false)");
            }

            Subject.RepairIfNeeded(Mocker.Resolve<IMainDatabase>(), "main");

            using (var connection = Mocker.Resolve<IMainDatabase>().OpenConnection())
            {
                connection.ExecuteScalar<long>("SELECT nextval('public.\"Tags_Id_seq\"'::regclass)")
                    .Should()
                    .Be(101);
            }
        }

        [Test]
        public void should_not_throw_for_sqlite()
        {
            if (Db.DatabaseType != DatabaseType.SQLite)
            {
                return;
            }

            Assert.DoesNotThrow(() => Subject.RepairIfNeeded(Mocker.Resolve<IMainDatabase>(), "main"));
        }

        private IPostgresSequenceRepairService Subject => Mocker.Resolve<IPostgresSequenceRepairService>();
    }
}
