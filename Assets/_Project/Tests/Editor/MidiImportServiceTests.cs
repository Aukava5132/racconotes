using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Racconotes.Application.Midi;
using Racconotes.Domain.Entities;
using Racconotes.Infrastructure.Database;
using Racconotes.Infrastructure.Repositories;
using SQLite;

namespace Racconotes.Tests
{
    /// <summary>
    /// Сквозной импорт MIDI → БД на in-memory SQLite: запись трека с метаданными, нот с
    /// последовательным индексом и валидной аппликатурой, срабатывание триггера диапазона клавиш,
    /// а также путь ImportFromBytes (парсер + персистентность вместе).
    /// </summary>
    public class MidiImportServiceTests
    {
        private SQLiteConnection _conn;
        private MidiImportService _service;

        [SetUp]
        public void SetUp()
        {
            _conn = DatabaseConnectionFactory.OpenInMemory();
            DatabaseInitializer.ApplySchema(_conn);
            _service = new MidiImportService(
                new SqliteTrackRepository(_conn),
                new SqliteNoteRepository(_conn));
        }

        [TearDown]
        public void TearDown() => _conn?.Dispose();

        private static MidiParseResult ThreeNotes() => new MidiParseResult(
            new List<RawMidiNote>
            {
                new RawMidiNote(48, 0.0, 0.5), // левая рука (< 60)
                new RawMidiNote(60, 0.5, 0.5), // правая
                new RawMidiNote(72, 1.0, 0.5)  // правая
            },
            bpm: 120, tonality: "C", ticksPerQuarterNote: 480);

        [Test]
        public void Import_WritesTrackRow_WithMetadata()
        {
            int trackId = _service.Import(ThreeNotes(), "moonlight.mid", composer: "Beethoven");

            Assert.Greater(trackId, 0);
            Assert.AreEqual("moonlight", _conn.ExecuteScalar<string>(
                "SELECT title FROM MidiTracks WHERE track_id=?;", trackId));
            Assert.AreEqual("Beethoven", _conn.ExecuteScalar<string>(
                "SELECT composer FROM MidiTracks WHERE track_id=?;", trackId));
            Assert.AreEqual(120.0, _conn.ExecuteScalar<double>(
                "SELECT bpm FROM MidiTracks WHERE track_id=?;", trackId), 1e-9);
            Assert.AreEqual("C", _conn.ExecuteScalar<string>(
                "SELECT tonality FROM MidiTracks WHERE track_id=?;", trackId));

            double difficulty = _conn.ExecuteScalar<double>(
                "SELECT difficulty FROM MidiTracks WHERE track_id=?;", trackId);
            Assert.GreaterOrEqual(difficulty, 1.0);
            Assert.LessOrEqual(difficulty, 10.0);

            double density = _conn.ExecuteScalar<double>(
                "SELECT note_density FROM MidiTracks WHERE track_id=?;", trackId);
            Assert.Greater(density, 0.0);
        }

        [Test]
        public void Import_WritesNotes_SequentialIndex_ValidHandFinger()
        {
            int trackId = _service.Import(ThreeNotes(), "test.mid");

            Assert.AreEqual(3, _conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM Notes WHERE track_id=?;", trackId));

            List<Note> notes = _conn.Query<Note>(
                @"SELECT note_id AS NoteId, track_id AS TrackId, note_index AS NoteIndex,
                         midi_number AS MidiNumber, start_time AS StartTime, duration AS Duration,
                         hand AS Hand, finger AS Finger
                  FROM Notes WHERE track_id=? ORDER BY note_index;", trackId);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, notes.Select(n => n.NoteIndex).ToList());
            Assert.AreEqual("left", notes[0].Hand);  // midi 48 < 60
            Assert.AreEqual("right", notes[1].Hand); // midi 60
            Assert.IsTrue(notes.All(n => n.Finger >= 1 && n.Finger <= 5));
        }

        [Test]
        public void Import_FiresTrigger_FillsKeyRange()
        {
            int trackId = _service.Import(ThreeNotes(), "test.mid");

            // Триггер update_track_range: MAX(72) - MIN(48) = 24.
            int range = _conn.ExecuteScalar<int>(
                "SELECT min_required_keys FROM MidiTracks WHERE track_id=?;", trackId);
            Assert.AreEqual(24, range);
        }

        [Test]
        public void ImportFromBytes_ParsesAndPersists()
        {
            byte[] midi = TestMidi.Format0(480,
                TestMidi.Tempo(0, 500_000),
                TestMidi.NoteOn(0, 0, 64, 100),
                TestMidi.NoteOff(480, 0, 64, 0),
                TestMidi.EndOfTrack(0));

            int trackId = _service.ImportFromBytes(midi, "single.mid");

            Assert.AreEqual(1, _conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM Notes WHERE track_id=?;", trackId));
            Assert.AreEqual(64, _conn.ExecuteScalar<int>(
                "SELECT midi_number FROM Notes WHERE track_id=?;", trackId));
            Assert.AreEqual(120.0, _conn.ExecuteScalar<double>(
                "SELECT bpm FROM MidiTracks WHERE track_id=?;", trackId), 1e-9);
        }
    }
}
