using NUnit.Framework;
using Racconotes.Infrastructure.Database;
using SQLite;

namespace Racconotes.Tests
{
    /// <summary>
    /// Схема §2.1-2.7 на in-memory БД: все таблицы создаются, работают FK ON DELETE CASCADE,
    /// CHECK-ограничения и триггеры (§2.6). Доказывает, что БД — «активный компонент».
    /// </summary>
    public class SqliteSchemaTests
    {
        private SQLiteConnection _conn;

        [SetUp]
        public void SetUp()
        {
            _conn = DatabaseConnectionFactory.OpenInMemory();
            DatabaseInitializer.ApplySchema(_conn);
        }

        [TearDown]
        public void TearDown() => _conn?.Dispose();

        [Test]
        public void Schema_Applies_AllTablesExist()
        {
            int tables = _conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';");
            Assert.AreEqual(15, tables); // §2.8: 15 таблиц
        }

        [Test]
        public void SchemaVersion_IsRecorded()
        {
            int version = _conn.ExecuteScalar<int>("SELECT version_number FROM SchemaVersion;");
            Assert.AreEqual(SqlSchema.Version, version);
        }

        [Test]
        public void ForeignKey_Cascade_DeletesNotes()
        {
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm) VALUES (1,'T',120);");
            _conn.Execute("INSERT INTO Notes(track_id, midi_number, start_time, hand, finger) VALUES (1,60,0,'left',1);");

            _conn.Execute("DELETE FROM MidiTracks WHERE track_id=1;");

            int notes = _conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Notes WHERE track_id=1;");
            Assert.AreEqual(0, notes); // CASCADE сработал => PRAGMA foreign_keys=ON активен
        }

        [Test]
        public void Check_Rejects_BadHand()
        {
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm) VALUES (1,'T',120);");
            Assert.Throws<SQLiteException>(() => _conn.Execute(
                "INSERT INTO Notes(track_id, midi_number, start_time, hand, finger) VALUES (1,60,0,'middle',1);"));
        }

        [Test]
        public void Check_Rejects_BadFinger()
        {
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm) VALUES (1,'T',120);");
            Assert.Throws<SQLiteException>(() => _conn.Execute(
                "INSERT INTO Notes(track_id, midi_number, start_time, hand, finger) VALUES (1,60,0,'left',9);"));
        }

        [Test]
        public void Check_Rejects_BadJudgement()
        {
            _conn.Execute("INSERT INTO Users(user_id, username) VALUES (1,'u');");
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm) VALUES (1,'T',120);");
            _conn.Execute("INSERT INTO Notes(note_id, track_id, midi_number, start_time, hand, finger) VALUES (1,1,60,0,'left',1);");
            _conn.Execute("INSERT INTO GameResults(result_id, user_id, track_id, accuracy_percent) VALUES (1,1,1,80);");

            Assert.Throws<SQLiteException>(() => _conn.Execute(
                "INSERT INTO HitEvents(result_id, note_id, judgement) VALUES (1,1,'awesome');"));
        }

        [Test]
        public void Trigger_UpdateUserStats_IncrementsGamesPlayed()
        {
            _conn.Execute("INSERT INTO Users(user_id, username) VALUES (1,'u');");
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm) VALUES (1,'T',120);");

            _conn.Execute("INSERT INTO GameResults(user_id, track_id, accuracy_percent) VALUES (1,1,80);");

            Assert.AreEqual(1, _conn.ExecuteScalar<int>("SELECT games_played FROM Users WHERE user_id=1;"));
            Assert.AreEqual(80.0, _conn.ExecuteScalar<double>("SELECT total_score FROM Users WHERE user_id=1;"), 1e-9);
        }

        [Test]
        public void Trigger_UpdateTrackRange_SetsOctaveRange()
        {
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm) VALUES (1,'T',120);");
            _conn.Execute("INSERT INTO Notes(track_id, midi_number, start_time, hand, finger) VALUES (1,60,0,'left',1);");
            _conn.Execute("INSERT INTO Notes(track_id, midi_number, start_time, hand, finger) VALUES (1,72,1,'right',2);");

            Assert.AreEqual(12, _conn.ExecuteScalar<int>("SELECT min_required_keys FROM MidiTracks WHERE track_id=1;"));
            string range = _conn.ExecuteScalar<string>("SELECT recommended_octave_range FROM MidiTracks WHERE track_id=1;");
            Assert.IsFalse(string.IsNullOrEmpty(range));
        }
    }
}
