using System;
using System.Linq;
using NUnit.Framework;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;
using Racconotes.Domain.Stats;
using Racconotes.Infrastructure.Database;
using Racconotes.Infrastructure.Repositories;
using SQLite;

namespace Racconotes.Tests
{
    /// <summary>
    /// Сложные SQL-запросы §2.4 (оконные функции, подзапросы, агрегаты) на in-memory БД.
    /// Значения зеркалят <c>StatsAnalyzerTests</c> (in-memory) — кросс-валидация SQL ↔ C#,
    /// сильный аргумент для защиты «БД как активный компонент».
    /// </summary>
    public class SqliteStatsQueriesTests
    {
        private SQLiteConnection _conn;
        private SqliteNoteRepository _noteRepo;
        private SqliteTrackRepository _trackRepo;
        private SqliteResultRepository _resultRepo;
        private SqliteStatsQueries _stats;

        [SetUp]
        public void SetUp()
        {
            _conn = DatabaseConnectionFactory.OpenInMemory();
            DatabaseInitializer.ApplySchema(_conn);
            _conn.Execute("INSERT INTO Users(user_id, username) VALUES (1,'tester');");
            _conn.Execute("INSERT INTO MidiTracks(track_id, title, bpm) VALUES (1,'Base',120);");

            _noteRepo = new SqliteNoteRepository(_conn);
            _trackRepo = new SqliteTrackRepository(_conn);
            _resultRepo = new SqliteResultRepository(_conn);
            _stats = new SqliteStatsQueries(_conn);
        }

        [TearDown]
        public void TearDown() => _conn?.Dispose();

        private int AddNote(int midi, string hand, int finger)
        {
            var n = new Note { TrackId = 1, MidiNumber = midi, StartTime = 0, Hand = hand, Finger = finger };
            _noteRepo.AddNote(n);
            return n.NoteId;
        }

        private int AddResult(int trackId, DateTime when, double accuracy)
        {
            var r = new GameResult { UserId = 1, TrackId = trackId, PlayedAt = when, AccuracyPercent = accuracy };
            _resultRepo.SaveGameResult(r);
            return r.ResultId;
        }

        private void AddHit(int resultId, int noteId, Judgement j, int deltaMs = 0) =>
            _resultRepo.SaveHitEvents(new[]
            {
                new HitEvent { ResultId = resultId, NoteId = noteId, Judgement = j, DeltaMs = deltaMs, ActualTime = deltaMs }
            });

        [Test]
        public void GetAccuracyProgress_ComputesImprovementViaLagOver()
        {
            AddResult(1, new DateTime(2026, 6, 1), 70);
            AddResult(1, new DateTime(2026, 6, 2), 85);
            AddResult(1, new DateTime(2026, 6, 3), 90);

            var progress = _stats.GetAccuracyProgress(1, 1);

            Assert.AreEqual(3, progress.Count);
            Assert.IsNull(progress[0].PrevAccuracy);
            Assert.IsNull(progress[0].Improvement);
            Assert.AreEqual(70.0, progress[1].PrevAccuracy);
            Assert.AreEqual(15.0, progress[1].Improvement, 1e-9); // 85 − 70
            Assert.AreEqual(5.0, progress[2].Improvement, 1e-9);  // 90 − 85
        }

        [Test]
        public void GetWeakSpots_OrdersByMissCountDesc()
        {
            int f4 = AddNote(65, "right", 3); // F4
            int c4 = AddNote(60, "left", 1);
            int r = AddResult(1, new DateTime(2026, 6, 1), 50);

            AddHit(r, f4, Judgement.Miss);
            AddHit(r, f4, Judgement.Miss);   // нота F4: 2 промаха
            AddHit(r, c4, Judgement.Miss);   // нота C4: 1 промах
            AddHit(r, c4, Judgement.Perfect); // попадание не считается

            var weak = _stats.GetWeakSpots(1);

            Assert.AreEqual(65, weak[0].MidiNumber);
            Assert.AreEqual(2, weak[0].MissCount);
            Assert.AreEqual(1, weak[1].MissCount);
        }

        [Test]
        public void GetAverageLatency_AveragesGoodAndBadPerFinger()
        {
            int a = AddNote(60, "right", 2);
            int b = AddNote(60, "right", 2);
            int r = AddResult(1, new DateTime(2026, 6, 1), 60);

            AddHit(r, a, Judgement.Good, deltaMs: 60);
            AddHit(r, b, Judgement.Bad, deltaMs: 140);
            AddHit(r, a, Judgement.Perfect, deltaMs: 0); // Perfect исключён

            var latency = _stats.GetAverageLatency(1);

            LatencyByFinger rightF2 = latency.Single(l => l.Hand == "right" && l.Finger == 2);
            Assert.AreEqual(100.0, rightF2.AvgDeltaMs, 1e-9); // (60+140)/2
            Assert.AreEqual(2, rightF2.HitCount);
        }

        [Test]
        public void RecommendTracks_ExcludesRecentlyPlayed_AndFitsLevel()
        {
            int trackA = _trackRepo.AddTrack(new MidiTrack { Title = "Recent", Bpm = 100, Difficulty = 6.0 });
            int trackB = _trackRepo.AddTrack(new MidiTrack { Title = "Fresh", Bpm = 100, Difficulty = 7.0 });

            // Недавняя игра по trackA (порог = средняя точность/10 = 5.0; оба трека ≥ 5).
            AddResult(trackA, DateTime.UtcNow.AddDays(-2), 50);

            var recommended = _stats.RecommendTracks(1);
            var ids = recommended.Select(t => t.TrackId).ToList();

            Assert.Contains(trackB, ids);            // свежий и по уровню
            CollectionAssert.DoesNotContain(ids, trackA); // исключён как недавно игранный
        }
    }
}
