using NUnit.Framework;
using Racconotes.Application.Scoring;
using Racconotes.Domain.Enums;

namespace Racconotes.Tests
{
    /// <summary>
    /// Граничные значения модели точности (§1.3). Проверяют корректность порогов
    /// 50/100/200 мс (включительно слева) и таблицу очков — ядро доказательства
    /// корректности «активной» логики для защиты.
    /// </summary>
    public class ScoreModelTests
    {
        private readonly ScoreModel _model = new ScoreModel();

        [TestCase(0.0, Judgement.Perfect)]
        [TestCase(50.0, Judgement.Perfect)]   // ровно на границе → Perfect (Δ ≤ 50)
        [TestCase(50.0001, Judgement.Good)]
        [TestCase(100.0, Judgement.Good)]     // граница → Good (Δ ≤ 100)
        [TestCase(100.0001, Judgement.Bad)]
        [TestCase(200.0, Judgement.Bad)]      // граница → Bad (Δ ≤ 200)
        [TestCase(200.0001, Judgement.Miss)]
        [TestCase(500.0, Judgement.Miss)]
        public void Judge_RespectsWindowBoundaries(double deltaMs, Judgement expected)
        {
            Assert.AreEqual(expected, _model.Judge(deltaMs));
        }

        [TestCase(-50.0, Judgement.Perfect)]  // раннее нажатие судится по модулю
        [TestCase(-150.0, Judgement.Bad)]
        [TestCase(-250.0, Judgement.Miss)]
        public void Judge_UsesAbsoluteDelta(double deltaMs, Judgement expected)
        {
            Assert.AreEqual(expected, _model.Judge(deltaMs));
        }

        [TestCase(Judgement.Perfect, 300)]
        [TestCase(Judgement.Good, 100)]
        [TestCase(Judgement.Bad, 50)]
        [TestCase(Judgement.Miss, 0)]
        public void ScoreFor_MatchesScoringTable(Judgement judgement, int expectedScore)
        {
            Assert.AreEqual(expectedScore, _model.ScoreFor(judgement));
        }

        [Test]
        public void Judge_RespectsCustomWindows()
        {
            var tight = new ScoreModel(new JudgementWindows(perfectMs: 20, goodMs: 40, badMs: 80));
            Assert.AreEqual(Judgement.Perfect, tight.Judge(20));
            Assert.AreEqual(Judgement.Good, tight.Judge(40));
            Assert.AreEqual(Judgement.Miss, tight.Judge(100));
        }
    }
}
