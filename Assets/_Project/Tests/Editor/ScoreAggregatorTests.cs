using System;
using NUnit.Framework;
using Racconotes.Application.Scoring;
using Racconotes.Domain.Enums;

namespace Racconotes.Tests
{
    /// <summary>
    /// Агрегация результата сессии: счётчики, общая точность %, комбо (§1.3).
    /// </summary>
    public class ScoreAggregatorTests
    {
        [Test]
        public void Counters_AggregateByJudgement()
        {
            var agg = new ScoreAggregator();
            agg.Register(Judgement.Perfect);
            agg.Register(Judgement.Perfect);
            agg.Register(Judgement.Good);
            agg.Register(Judgement.Bad);
            agg.Register(Judgement.Miss);

            Assert.AreEqual(2, agg.Count300);
            Assert.AreEqual(1, agg.Count100);
            Assert.AreEqual(1, agg.Count50);
            Assert.AreEqual(1, agg.CountMiss);
            Assert.AreEqual(5, agg.TotalNotes);
            Assert.AreEqual(2 * 300 + 100 + 50, agg.TotalScore);
        }

        [Test]
        public void AccuracyPercent_AllPerfect_Is100()
        {
            var agg = new ScoreAggregator();
            agg.Register(Judgement.Perfect);
            agg.Register(Judgement.Perfect);

            Assert.AreEqual(100.0, agg.AccuracyPercent, 1e-9);
        }

        [Test]
        public void AccuracyPercent_PerfectPlusMiss_Is50()
        {
            var agg = new ScoreAggregator();
            agg.Register(Judgement.Perfect); // 300 из 600 максимум
            agg.Register(Judgement.Miss);

            Assert.AreEqual(50.0, agg.AccuracyPercent, 1e-9);
        }

        [Test]
        public void AccuracyPercent_NoNotes_IsZero_NoDivideByZero()
        {
            var agg = new ScoreAggregator();
            Assert.AreEqual(0.0, agg.AccuracyPercent, 1e-9);
        }

        [Test]
        public void Combo_GrowsOnHits_BreaksOnMiss()
        {
            var agg = new ScoreAggregator();
            agg.Register(Judgement.Perfect); // combo 1
            agg.Register(Judgement.Good);    // combo 2
            agg.Register(Judgement.Bad);     // combo 3 (Bad не рвёт комбо)
            Assert.AreEqual(3, agg.CurrentCombo);

            agg.Register(Judgement.Miss);    // обнуление
            Assert.AreEqual(0, agg.CurrentCombo);

            agg.Register(Judgement.Perfect); // combo 1
            Assert.AreEqual(1, agg.CurrentCombo);
            Assert.AreEqual(3, agg.MaxCombo); // максимум сохранён
        }

        [Test]
        public void BuildResult_CopiesAggregatedValues()
        {
            var agg = new ScoreAggregator();
            agg.Register(Judgement.Perfect);
            agg.Register(Judgement.Miss);

            var when = new DateTime(2026, 6, 10, 12, 0, 0);
            var result = agg.BuildResult(userId: 7, trackId: 3, playedAt: when);

            Assert.AreEqual(7, result.UserId);
            Assert.AreEqual(3, result.TrackId);
            Assert.AreEqual(when, result.PlayedAt);
            Assert.AreEqual(1, result.Score300);
            Assert.AreEqual(1, result.MissCount);
            Assert.AreEqual(50.0, result.AccuracyPercent, 1e-9);
            Assert.AreEqual(1, result.MaxCombo);
        }
    }
}
