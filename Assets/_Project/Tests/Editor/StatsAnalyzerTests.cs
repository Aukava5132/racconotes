using System;
using System.Linq;
using NUnit.Framework;
using Racconotes.Application.Stats;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;
using Racconotes.Domain.Stats;

namespace Racconotes.Tests
{
    /// <summary>
    /// StatsAnalyzer — in-memory аналоги SQL §2.4: слабые места, средняя задержка, прогресс.
    /// </summary>
    public class StatsAnalyzerTests
    {
        private readonly StatsAnalyzer _stats = new StatsAnalyzer();

        private static Note Note(int id, int midi, string hand, int finger) => new Note
        {
            NoteId = id,
            MidiNumber = midi,
            Hand = hand,
            Finger = finger
        };

        private static HitEvent Hit(int noteId, Judgement j, int deltaMs = 0) => new HitEvent
        {
            NoteId = noteId,
            Judgement = j,
            DeltaMs = deltaMs
        };

        [Test]
        public void GetWeakSpots_OrdersByMissCountDesc()
        {
            var notes = new[]
            {
                Note(1, 65, "right", 3), // F4
                Note(2, 60, "left", 1)
            };
            var hits = new[]
            {
                Hit(1, Judgement.Miss), Hit(1, Judgement.Miss), // нота1: 2 промаха
                Hit(2, Judgement.Miss),                          // нота2: 1 промах
                Hit(2, Judgement.Perfect)                        // попадание не считается
            };

            var weak = _stats.GetWeakSpots(hits, notes);

            Assert.AreEqual(65, weak[0].MidiNumber); // больше всего промахов
            Assert.AreEqual(2, weak[0].MissCount);
            Assert.AreEqual(1, weak[1].MissCount);
        }

        [Test]
        public void GetWeakSpots_RespectsTopLimit()
        {
            var notes = new[] { Note(1, 60, "left", 1), Note(2, 62, "left", 2), Note(3, 64, "left", 3) };
            var hits = new[] { Hit(1, Judgement.Miss), Hit(2, Judgement.Miss), Hit(3, Judgement.Miss) };

            var weak = _stats.GetWeakSpots(hits, notes, top: 2);

            Assert.AreEqual(2, weak.Count);
        }

        [Test]
        public void GetAverageLatency_AveragesGoodAndBadPerFinger()
        {
            var notes = new[] { Note(1, 60, "right", 2), Note(2, 60, "right", 2) };
            var hits = new[]
            {
                Hit(1, Judgement.Good, deltaMs: 60),
                Hit(2, Judgement.Bad, deltaMs: 140),
                Hit(1, Judgement.Perfect, deltaMs: 0) // Perfect исключён из задержки
            };

            var latency = _stats.GetAverageLatency(hits, notes);

            var rightF2 = latency.Single(l => l.Hand == "right" && l.Finger == 2);
            Assert.AreEqual(100.0, rightF2.AvgDeltaMs, 1e-9); // (60+140)/2
            Assert.AreEqual(2, rightF2.HitCount);
        }

        [Test]
        public void GetAccuracyProgress_ComputesImprovementViaPrevious()
        {
            var results = new[]
            {
                new GameResult { TrackId = 5, PlayedAt = new DateTime(2026, 6, 1), AccuracyPercent = 70 },
                new GameResult { TrackId = 5, PlayedAt = new DateTime(2026, 6, 2), AccuracyPercent = 85 },
                new GameResult { TrackId = 9, PlayedAt = new DateTime(2026, 6, 3), AccuracyPercent = 10 } // другой трек
            };

            var progress = _stats.GetAccuracyProgress(results, trackId: 5);

            Assert.AreEqual(2, progress.Count);
            Assert.IsNull(progress[0].PrevAccuracy);
            Assert.IsNull(progress[0].Improvement);
            Assert.AreEqual(70.0, progress[1].PrevAccuracy);
            Assert.AreEqual(15.0, progress[1].Improvement, 1e-9); // 85 − 70
        }
    }
}
