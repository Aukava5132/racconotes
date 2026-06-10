using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Racconotes.Application;
using Racconotes.Application.Engine;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;

namespace Racconotes.Tests
{
    /// <summary>
    /// Сопоставление нажатий с нотами в GameEngine (§1.3): попадания, промахи,
    /// лишние нажатия, выбор ближайшей ноты, порядок комбо и переходы FSM.
    /// </summary>
    public class GameEngineMatchingTests
    {
        private static readonly DateTime When = new DateTime(2026, 6, 10);

        // StartTime в секундах (как в БД); внутри движок переводит в мс.
        private static Note Note(int id, int midi, double startSec, int index = 0) => new Note
        {
            NoteId = id,
            MidiNumber = midi,
            StartTime = startSec,
            NoteIndex = index
        };

        private static SessionEvaluation Run(IReadOnlyList<Note> notes, IReadOnlyList<InputEvent> inputs)
            => new GameEngine().Run(notes, inputs, userId: 1, trackId: 1, playedAt: When);

        [Test]
        public void ExactHit_IsPerfect()
        {
            var notes = new[] { Note(1, 60, 1.0) };
            var inputs = new[] { new InputEvent(1000.0, 60) };

            var eval = Run(notes, inputs);

            Assert.AreEqual(1, eval.HitEvents.Count);
            Assert.AreEqual(Judgement.Perfect, eval.HitEvents[0].Judgement);
            Assert.AreEqual(0, eval.HitEvents[0].DeltaMs);
        }

        [Test]
        public void TwoInputsSamePitch_MapToDistinctNotes()
        {
            var notes = new[] { Note(1, 60, 1.0, 0), Note(2, 60, 2.0, 1) };
            var inputs = new[] { new InputEvent(1000.0, 60), new InputEvent(2000.0, 60) };

            var eval = Run(notes, inputs);

            // Каждая нота попалась ровно один раз, обе Perfect.
            Assert.That(eval.HitEvents.Select(h => h.NoteId), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.IsTrue(eval.HitEvents.All(h => h.Judgement == Judgement.Perfect));
        }

        [Test]
        public void MissingInput_ProducesMiss()
        {
            var notes = new[] { Note(1, 60, 1.0) };
            var eval = Run(notes, Array.Empty<InputEvent>());

            Assert.AreEqual(1, eval.HitEvents.Count);
            Assert.AreEqual(Judgement.Miss, eval.HitEvents[0].Judgement);
            Assert.IsNaN(eval.HitEvents[0].ActualTime);
        }

        [Test]
        public void ExtraInput_OutsideWindow_IsIgnored()
        {
            var notes = new[] { Note(1, 60, 1.0) };
            // Один точный ввод + один далёкий лишний.
            var inputs = new[] { new InputEvent(1000.0, 60), new InputEvent(5000.0, 60) };

            var eval = Run(notes, inputs);

            // Число HitEvent равно числу нот — лишнее нажатие не порождает событий.
            Assert.AreEqual(1, eval.HitEvents.Count);
            Assert.AreEqual(Judgement.Perfect, eval.HitEvents[0].Judgement);
        }

        [Test]
        public void WrongPitch_DoesNotMatch()
        {
            var notes = new[] { Note(1, 60, 1.0) };
            var inputs = new[] { new InputEvent(1000.0, 62) }; // другая высота

            var eval = Run(notes, inputs);

            Assert.AreEqual(Judgement.Miss, eval.HitEvents[0].Judgement);
        }

        [Test]
        public void DeltaBeyondBadWindow_IsMiss()
        {
            var notes = new[] { Note(1, 60, 1.0) };
            var inputs = new[] { new InputEvent(1250.0, 60) }; // Δ = 250 мс > 200

            var eval = Run(notes, inputs);

            Assert.AreEqual(Judgement.Miss, eval.HitEvents[0].Judgement);
            Assert.IsNaN(eval.HitEvents[0].ActualTime); // ввод не сопоставился, нота не нажата
        }

        [Test]
        public void NearestNote_IsChosen()
        {
            // Две ноты одной высоты близко; один ввод ближе ко второй.
            var notes = new[] { Note(1, 60, 1.0, 0), Note(2, 60, 1.12, 1) };
            var inputs = new[] { new InputEvent(1100.0, 60) }; // Δ к ноте1=100, к ноте2=20

            var eval = Run(notes, inputs);

            var hitNote2 = eval.HitEvents.Single(h => h.NoteId == 2);
            var hitNote1 = eval.HitEvents.Single(h => h.NoteId == 1);
            Assert.AreEqual(Judgement.Perfect, hitNote2.Judgement); // ближайшая попалась
            Assert.AreEqual(Judgement.Miss, hitNote1.Judgement);
        }

        [Test]
        public void MaxCombo_CountedInNoteOrder_NotInputOrder()
        {
            // Ноты по порядку: 0,1,2. Вторая (index1) будет пропущена → комбо рвётся посередине.
            var notes = new[] { Note(1, 60, 1.0, 0), Note(2, 62, 2.0, 1), Note(3, 64, 3.0, 2) };
            // Вводы приходят не по порядку нот, ноту2 не нажимаем.
            var inputs = new[] { new InputEvent(3000.0, 64), new InputEvent(1000.0, 60) };

            var eval = Run(notes, inputs);

            // Perfect, Miss, Perfect → максимальное комбо = 1.
            Assert.AreEqual(1, eval.Result.MaxCombo);
            Assert.AreEqual(2, eval.Result.Score300);
            Assert.AreEqual(1, eval.Result.MissCount);
        }

        [Test]
        public void State_IsEvaluating_AfterRun()
        {
            var engine = new GameEngine();
            engine.Run(new[] { Note(1, 60, 1.0) }, new[] { new InputEvent(1000.0, 60) }, 1, 1, When);

            Assert.AreEqual(GameSessionState.Evaluating, engine.State);
        }
    }
}
