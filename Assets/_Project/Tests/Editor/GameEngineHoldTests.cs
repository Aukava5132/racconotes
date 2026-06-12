using System;
using System.Collections.Generic;
using NUnit.Framework;
using Racconotes.Application;
using Racconotes.Application.Engine;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;

namespace Racconotes.Tests
{
    /// <summary>
    /// Realtime-механика длинных нот-удержаний (osu!mania, упрощённая оценка одной нотой):
    /// голова судится по таймингу нажатия; удержание до хвоста сохраняет оценку, раннее
    /// отпускание делает ноту Miss и рвёт комбо. Тапы (Duration ≤ порога) холдами не считаются.
    /// Проверяется пошаговый путь Begin/OnInput/OnRelease/TickHolds/Finish; батч Run() не затронут.
    /// </summary>
    public class GameEngineHoldTests
    {
        private static readonly DateTime When = new DateTime(2026, 6, 13);

        // Длинная нота-удержание (Duration > HoldRules.MinDurationSeconds).
        private static Note Hold(int id, int midi, double startSec, double durationSec) => new Note
        {
            NoteId = id, MidiNumber = midi, StartTime = startSec, Duration = durationSec, NoteIndex = 0
        };

        // releaseMs для ноты start=1.0с, dur=1.0с → конец удержания = 2000 мс.
        private static GameEngine Begun(Note note)
        {
            var engine = new GameEngine();
            engine.Begin(new[] { note });
            return engine;
        }

        [Test]
        public void HeadPress_OnHold_IsJudgedByTiming()
        {
            GameEngine engine = Begun(Hold(1, 60, 1.0, 1.0));
            HitEvent head = engine.OnInput(new InputEvent(1000.0, 60)); // Δ=0 → Perfect

            Assert.IsNotNull(head);
            Assert.AreEqual(Judgement.Perfect, head.Judgement);
        }

        [Test]
        public void HeldThroughEnd_TickResolvesAsHeadJudgement()
        {
            GameEngine engine = Begun(Hold(1, 60, 1.0, 1.0)); // конец = 2000 мс, окно Bad = 200
            engine.OnInput(new InputEvent(1000.0, 60));

            // До (конец + окно) удержание ещё активно — тик ничего не возвращает.
            Assert.AreEqual(0, engine.TickHolds(2100.0).Count);

            // На (конец + окно) удержание завершается успехом с оценкой головы.
            IReadOnlyList<HitEvent> resolved = engine.TickHolds(2200.0);
            Assert.AreEqual(1, resolved.Count);
            Assert.AreEqual(Judgement.Perfect, resolved[0].Judgement);

            SessionEvaluation eval = engine.Finish(1, 1, When);
            Assert.AreEqual(1, eval.Result.Score300);
            Assert.AreEqual(0, eval.Result.MissCount);
        }

        [Test]
        public void ReleasedEarly_BecomesMiss_AndBreaksCombo()
        {
            GameEngine engine = Begun(Hold(1, 60, 1.0, 1.0)); // конец = 2000, ранний порог = 1800
            engine.OnInput(new InputEvent(1000.0, 60));

            HitEvent r = engine.OnRelease(60, 1500.0); // 1500 < 1800 → срыв удержания
            Assert.IsNotNull(r);
            Assert.AreEqual(Judgement.Miss, r.Judgement);

            SessionEvaluation eval = engine.Finish(1, 1, When);
            Assert.AreEqual(1, eval.Result.MissCount);
            Assert.AreEqual(0, eval.Result.Score300);
            Assert.AreEqual(0, eval.Result.MaxCombo);
        }

        [Test]
        public void ReleasedNearEnd_KeepsHeadJudgement()
        {
            GameEngine engine = Begun(Hold(1, 60, 1.0, 1.0)); // ранний порог = 1800
            engine.OnInput(new InputEvent(1000.0, 60));

            HitEvent r = engine.OnRelease(60, 1850.0); // 1850 ≥ 1800 → засчитано
            Assert.AreEqual(Judgement.Perfect, r.Judgement);

            SessionEvaluation eval = engine.Finish(1, 1, When);
            Assert.AreEqual(1, eval.Result.Score300);
            Assert.AreEqual(0, eval.Result.MissCount);
        }

        [Test]
        public void ShortNote_IsTap_NotTrackedAsHold()
        {
            GameEngine engine = Begun(Hold(1, 60, 1.0, 0.3)); // 0.3 ≤ порог 0.5 → тап
            HitEvent head = engine.OnInput(new InputEvent(1000.0, 60));
            Assert.AreEqual(Judgement.Perfect, head.Judgement);

            // Тап не порождает активного удержания: ни отпускание, ни тик ничего не финализируют.
            Assert.IsNull(engine.OnRelease(60, 9999.0));
            Assert.AreEqual(0, engine.TickHolds(9999.0).Count);

            SessionEvaluation eval = engine.Finish(1, 1, When);
            Assert.AreEqual(1, eval.Result.Score300);
        }

        [Test]
        public void Release_WithoutActiveHold_ReturnsNull()
        {
            GameEngine engine = Begun(Hold(1, 60, 1.0, 1.0));
            // Голову не нажимали — активного удержания нет.
            Assert.IsNull(engine.OnRelease(60, 1500.0));
        }

        [Test]
        public void BatchRun_LongNote_UnaffectedByHoldLogic()
        {
            // Батч-путь (тесты/оффлайн) не использует отпускания/тики: длинная нота судится по голове.
            var notes = new[] { Hold(1, 60, 1.0, 1.0) };
            var inputs = new[] { new InputEvent(1000.0, 60) };

            SessionEvaluation eval = new GameEngine().Run(notes, inputs, 1, 1, When);
            Assert.AreEqual(1, eval.Result.Score300);
            Assert.AreEqual(0, eval.Result.MissCount);
        }
    }
}
