using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;
using Racconotes.Infrastructure.Database;
using Racconotes.Infrastructure.Repositories;
using SQLite;

namespace Racconotes.Tests
{
    /// <summary>
    /// SQLite-репозитории на in-memory БД: CRUD, фильтры, bulk-вставка, snake→Pascal маппинг,
    /// round-trip DateTime (тики) и enum Judgement ↔ TEXT.
    /// </summary>
    public class SqliteRepositoryTests
    {
        private SQLiteConnection _conn;
        private SqliteNoteRepository _noteRepo;
        private SqliteTrackRepository _trackRepo;
        private SqliteResultRepository _resultRepo;
        private SqliteUserSettingsRepository _settingsRepo;

        [SetUp]
        public void SetUp()
        {
            _conn = DatabaseConnectionFactory.OpenInMemory();
            DatabaseInitializer.ApplySchema(_conn);
            // Базовые user(1) и track(1) для тестов с FK на результаты/ноты.
            // bpm/difficulty намеренно вне типичных фильтров, чтобы базовый трек не попадал в выборки.
            _conn.Execute("INSERT INTO Users(user_id, username) VALUES (1,'tester');");
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm, difficulty, note_density) VALUES (1,'Base',1,1,1);");

            _noteRepo = new SqliteNoteRepository(_conn);
            _trackRepo = new SqliteTrackRepository(_conn);
            _resultRepo = new SqliteResultRepository(_conn);
            _settingsRepo = new SqliteUserSettingsRepository(_conn);
        }

        [TearDown]
        public void TearDown() => _conn?.Dispose();

        private static Note NewNote(int midi, double start, string hand = "right", int finger = 3) => new Note
        {
            TrackId = 1, MidiNumber = midi, StartTime = start, Duration = 0.5, Hand = hand, Finger = finger
        };

        // ----- INoteRepository -----

        [Test]
        public void AddNote_SetsNoteId_AndReadsBack()
        {
            var note = NewNote(64, 1.0);
            _noteRepo.AddNote(note);

            Assert.Greater(note.NoteId, 0);
            Note loaded = _noteRepo.GetNoteById(note.NoteId);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(64, loaded.MidiNumber);
        }

        [Test]
        public void BulkInsertNotes_InsertsAll_InTransaction()
        {
            var notes = Enumerable.Range(0, 100)
                .Select(i => NewNote(60 + (i % 12), i * 0.25))
                .ToList();

            _noteRepo.BulkInsertNotes(notes);

            Assert.AreEqual(100, _noteRepo.GetNotesForTrack(1).Count());
            Assert.IsTrue(notes.All(n => n.NoteId > 0)); // id проставлены каждой ноте
        }

        [Test]
        public void GetNotesForTrack_ReturnsOrderedByStartTime()
        {
            _noteRepo.AddNote(NewNote(67, 2.0));
            _noteRepo.AddNote(NewNote(60, 0.0));
            _noteRepo.AddNote(NewNote(64, 1.0));

            double[] times = _noteRepo.GetNotesForTrack(1).Select(n => n.StartTime).ToArray();
            CollectionAssert.AreEqual(new[] { 0.0, 1.0, 2.0 }, times);
        }

        [Test]
        public void GetNotesForTrack_MapsSnakeCaseToPascal()
        {
            _noteRepo.AddNote(NewNote(65, 3.5, hand: "left", finger: 4));

            Note n = _noteRepo.GetNotesForTrack(1).Single();
            // Все колонки snake_case подтянулись в свойства PascalCase через AS-алиасы.
            Assert.AreEqual(1, n.TrackId);
            Assert.AreEqual(65, n.MidiNumber);
            Assert.AreEqual(3.5, n.StartTime, 1e-9);
            Assert.AreEqual(0.5, n.Duration, 1e-9);
            Assert.AreEqual("left", n.Hand);
            Assert.AreEqual(4, n.Finger);
        }

        [Test]
        public void UpdateNoteFinger_Persists()
        {
            var note = NewNote(64, 1.0, hand: "right", finger: 3);
            _noteRepo.AddNote(note);

            _noteRepo.UpdateNoteFinger(note.NoteId, finger: 5, hand: "left");

            Note loaded = _noteRepo.GetNoteById(note.NoteId);
            Assert.AreEqual(5, loaded.Finger);
            Assert.AreEqual("left", loaded.Hand);
        }

        // ----- ITrackRepository -----

        [Test]
        public void AddTrack_ReturnsNewId_AndReadsBack()
        {
            int id = _trackRepo.AddTrack(new MidiTrack
            {
                Filename = "song.mid", Title = "Песня", Composer = "Аноним",
                Bpm = 90, Tonality = "G", Difficulty = 3.5, NoteDensity = 2.0
            });

            Assert.Greater(id, 0);
            MidiTrack loaded = _trackRepo.GetTrackById(id);
            Assert.AreEqual("Песня", loaded.Title);
            Assert.AreEqual(90, loaded.Bpm, 1e-9);
        }

        [Test]
        public void FilterByDifficulty_RespectsBounds()
        {
            _trackRepo.AddTrack(new MidiTrack { Title = "A", Bpm = 100, Difficulty = 2.0 });
            _trackRepo.AddTrack(new MidiTrack { Title = "B", Bpm = 100, Difficulty = 5.0 });
            _trackRepo.AddTrack(new MidiTrack { Title = "C", Bpm = 100, Difficulty = 8.0 });

            var titles = _trackRepo.FilterByDifficulty(4f, 9f).Select(t => t.Title).ToList();
            CollectionAssert.AreEquivalent(new[] { "B", "C" }, titles);
        }

        [Test]
        public void FilterByBpm_RespectsBounds()
        {
            _trackRepo.AddTrack(new MidiTrack { Title = "Slow", Bpm = 60, Difficulty = 3 });
            _trackRepo.AddTrack(new MidiTrack { Title = "Mid", Bpm = 120, Difficulty = 3 });
            _trackRepo.AddTrack(new MidiTrack { Title = "Fast", Bpm = 180, Difficulty = 3 });

            var titles = _trackRepo.FilterByBpm(100, 150).Select(t => t.Title).ToList();
            CollectionAssert.AreEquivalent(new[] { "Mid" }, titles);
        }

        [Test]
        public void DeleteTrack_CascadesNotes()
        {
            int id = _trackRepo.AddTrack(new MidiTrack { Title = "Doomed", Bpm = 100, Difficulty = 3 });
            _noteRepo.AddNote(new Note { TrackId = id, MidiNumber = 60, StartTime = 0, Hand = "left", Finger = 1 });

            _trackRepo.DeleteTrack(id);

            Assert.IsEmpty(_noteRepo.GetNotesForTrack(id));
        }

        [Test]
        public void DeleteTrack_WithPlayHistory_RemovesResultsAndHits()
        {
            // Сыгранный трек: есть ноты, результат и события попаданий. GameResults/HitEvents ссылаются
            // на трек/ноты БЕЗ ON DELETE CASCADE — раньше удаление падало «FOREIGN KEY constraint failed».
            int id = _trackRepo.AddTrack(new MidiTrack { Title = "Played", Bpm = 120, Difficulty = 3 });
            var note = NewNote(64, 0.0);
            note.TrackId = id;
            _noteRepo.AddNote(note);
            var result = new GameResult { UserId = 1, TrackId = id, PlayedAt = DateTime.UtcNow, AccuracyPercent = 100 };
            _resultRepo.SaveGameResult(result);
            _resultRepo.SaveHitEvents(new[]
            {
                new HitEvent { ResultId = result.ResultId, NoteId = note.NoteId, Judgement = Judgement.Perfect }
            });

            Assert.DoesNotThrow(() => _trackRepo.DeleteTrack(id));

            Assert.IsNull(_trackRepo.GetTrackById(id));
            Assert.IsEmpty(_noteRepo.GetNotesForTrack(id));
            Assert.AreEqual(0, _conn.ExecuteScalar<int>("SELECT COUNT(*) FROM GameResults WHERE track_id = ?;", id));
            Assert.AreEqual(0, _conn.ExecuteScalar<int>("SELECT COUNT(*) FROM HitEvents WHERE result_id = ?;", result.ResultId));
        }

        // ----- IResultRepository -----

        [Test]
        public void SaveGameResult_SetsResultId_AndRoundTripsDateTime()
        {
            var when = new DateTime(2026, 6, 10, 12, 30, 0);
            var result = new GameResult
            {
                UserId = 1, TrackId = 1, PlayedAt = when, AccuracyPercent = 87.5,
                Score300 = 5, Score100 = 1, Score50 = 0, MissCount = 0, MaxCombo = 6
            };

            _resultRepo.SaveGameResult(result);

            Assert.Greater(result.ResultId, 0);
            GameResult saved = _resultRepo.GetUserHistory(1).Single();
            Assert.AreEqual(when, saved.PlayedAt); // тики записаны и прочитаны без искажений
            Assert.AreEqual(87.5, saved.AccuracyPercent, 1e-9);
            Assert.AreEqual(5, saved.Score300);
        }

        [Test]
        public void SaveHitEvents_PersistsEnumAsText_AndReadsBackEnum()
        {
            var note = NewNote(64, 1.0);
            _noteRepo.AddNote(note);
            var result = new GameResult { UserId = 1, TrackId = 1, PlayedAt = DateTime.UtcNow, AccuracyPercent = 100 };
            _resultRepo.SaveGameResult(result);

            _resultRepo.SaveHitEvents(new[]
            {
                new HitEvent
                {
                    ResultId = result.ResultId, NoteId = note.NoteId,
                    ExpectedTime = 1000, ActualTime = 1005, DeltaMs = 5,
                    Judgement = Judgement.Perfect, FingerUsed = 3
                }
            });

            // В БД хранится строкой 'perfect' (CHECK прошёл).
            Assert.AreEqual("perfect", _conn.ExecuteScalar<string>("SELECT judgement FROM HitEvents;"));

            // И читается обратно как enum (Enum.Parse в sqlite-net).
            List<HitEvent> rows = _conn.Query<HitEvent>(
                @"SELECT hit_id AS HitId, result_id AS ResultId, note_id AS NoteId,
                         expected_time AS ExpectedTime, actual_time AS ActualTime, delta_ms AS DeltaMs,
                         judgement AS Judgement, finger_used AS FingerUsed
                  FROM HitEvents;");
            Assert.AreEqual(Judgement.Perfect, rows.Single().Judgement);
        }

        [Test]
        public void GetAverageAccuracy_AveragesPerTrack()
        {
            _resultRepo.SaveGameResult(new GameResult { UserId = 1, TrackId = 1, PlayedAt = new DateTime(2026, 6, 1), AccuracyPercent = 80 });
            _resultRepo.SaveGameResult(new GameResult { UserId = 1, TrackId = 1, PlayedAt = new DateTime(2026, 6, 2), AccuracyPercent = 90 });

            Assert.AreEqual(85f, _resultRepo.GetAverageAccuracy(1, 1), 1e-4f);
        }

        [Test]
        public void GetWeakSpots_ReturnsDictionaryKeyedByNote()
        {
            var note = NewNote(65, 1.0, hand: "right", finger: 4);
            _noteRepo.AddNote(note);
            var result = new GameResult { UserId = 1, TrackId = 1, PlayedAt = DateTime.UtcNow, AccuracyPercent = 0 };
            _resultRepo.SaveGameResult(result);
            _resultRepo.SaveHitEvents(new[]
            {
                new HitEvent { ResultId = result.ResultId, NoteId = note.NoteId, Judgement = Judgement.Miss },
                new HitEvent { ResultId = result.ResultId, NoteId = note.NoteId, Judgement = Judgement.Miss }
            });

            Dictionary<string, int> weak = _resultRepo.GetWeakSpots(1);
            Assert.AreEqual(2, weak["65/right/4"]);
        }

        // ----- IUserSettingsRepository -----

        [Test]
        public void GetSettings_ReturnsNull_WhenNoRow()
        {
            Assert.IsNull(_settingsRepo.GetSettings(1));
        }

        [Test]
        public void SaveSettings_Insert_RoundTripsLabelModes()
        {
            _settingsRepo.SaveSettings(new UserSettings
            {
                UserId = 1, PreferredHand = "auto",
                KeyLabelMode = "key", NoteLabelMode = "solfege"
            });

            UserSettings loaded = _settingsRepo.GetSettings(1);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("key", loaded.KeyLabelMode);
            Assert.AreEqual("solfege", loaded.NoteLabelMode);
        }

        [Test]
        public void SaveSettings_Upsert_UpdatesExistingRow_WithoutDuplicating()
        {
            _settingsRepo.SaveSettings(new UserSettings { UserId = 1, KeyLabelMode = "note", NoteLabelMode = "note" });
            _settingsRepo.SaveSettings(new UserSettings { UserId = 1, KeyLabelMode = "off", NoteLabelMode = "key" });

            Assert.AreEqual(1, _conn.ExecuteScalar<int>("SELECT COUNT(*) FROM UserSettings WHERE user_id = 1;"));
            UserSettings loaded = _settingsRepo.GetSettings(1);
            Assert.AreEqual("off", loaded.KeyLabelMode);
            Assert.AreEqual("key", loaded.NoteLabelMode);
        }

        [Test]
        public void SaveSettings_NullModes_StoredAsOff()
        {
            _settingsRepo.SaveSettings(new UserSettings { UserId = 1 }); // режимы не заданы

            UserSettings loaded = _settingsRepo.GetSettings(1);
            Assert.AreEqual("off", loaded.KeyLabelMode);
            Assert.AreEqual("off", loaded.NoteLabelMode);
        }

        [Test]
        public void EnsureCreated_Migrates_OldDbWithoutLabelColumns()
        {
            // Имитируем старый notes.db: схема есть (SchemaVersion), но UserSettings без новых колонок.
            using SQLiteConnection old = DatabaseConnectionFactory.OpenInMemory();
            old.Execute("CREATE TABLE SchemaVersion (version_id INTEGER PRIMARY KEY, version_number INTEGER, applied_at TEXT, description TEXT);");
            old.Execute("INSERT INTO SchemaVersion (version_number) VALUES (1);");
            old.Execute("CREATE TABLE Users (user_id INTEGER PRIMARY KEY, username TEXT);");
            old.Execute(@"CREATE TABLE UserSettings (
                user_id INTEGER PRIMARY KEY, visual_offset_ms INTEGER DEFAULT 0, audio_offset_ms INTEGER DEFAULT 0,
                min_bpm_filter INTEGER, max_bpm_filter INTEGER, preferred_hand TEXT DEFAULT 'auto',
                difficulty_filter_min REAL DEFAULT 1, difficulty_filter_max REAL DEFAULT 10);");
            old.Execute("INSERT INTO Users(user_id, username) VALUES (1,'demo');");
            old.Execute("INSERT INTO UserSettings(user_id, preferred_hand) VALUES (1,'auto');");

            DatabaseInitializer.EnsureCreated(old, seed: false); // должен добавить недостающие колонки

            // Чтение настроек больше не падает на «no such column key_label_mode»; ALTER ... DEFAULT 'off'
            // заполнил существующую строку.
            UserSettings s = new SqliteUserSettingsRepository(old).GetSettings(1);
            Assert.IsNotNull(s);
            Assert.AreEqual("off", s.KeyLabelMode);
            Assert.AreEqual("off", s.NoteLabelMode);
        }
    }
}
