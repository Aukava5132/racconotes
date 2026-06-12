using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Racconotes.Application;
using Racconotes.Application.Engine;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;
using Racconotes.Domain.Repositories;

namespace Racconotes.Tests
{
    /// <summary>
    /// Realtime-API сессии (Этап 3): BeginPlaying / PushInput / EndPlaying переиспользуют
    /// тот же GameEngine, что и пакетный PlaySelectedTrack — единый источник истины судейства.
    /// Проверяем FSM-переходы, живую обратную связь, персист результата+нажатий и эквивалентность
    /// пакетному прогону при тех же входах.
    /// </summary>
    public class SessionControllerRealtimeTests
    {
        private static readonly DateTime When = new DateTime(2026, 6, 10);

        private static IReadOnlyList<Note> DemoNotes() => new[]
        {
            new Note { NoteId = 1, TrackId = 5, NoteIndex = 0, MidiNumber = 60, StartTime = 1.0 },
            new Note { NoteId = 2, TrackId = 5, NoteIndex = 1, MidiNumber = 62, StartTime = 2.0 },
            new Note { NoteId = 3, TrackId = 5, NoteIndex = 2, MidiNumber = 64, StartTime = 3.0 }
        };

        [Test]
        public void BeginPlaying_LoadsNotes_AndEntersPlaying()
        {
            var noteRepo = new FakeNoteRepository();
            noteRepo.Notes.AddRange(DemoNotes());
            var controller = new SessionController(noteRepo, new FakeResultRepository());

            controller.SelectTrack(5);
            IReadOnlyList<Note> notes = controller.BeginPlaying();

            Assert.AreEqual(GameSessionState.Playing, controller.State);
            Assert.AreEqual(3, notes.Count);
            CollectionAssert.AreEquivalent(new[] { 60, 62, 64 }, notes.Select(n => n.MidiNumber));
        }

        [Test]
        public void BeginPlaying_WithoutSelect_Throws()
        {
            var controller = new SessionController(new FakeNoteRepository(), new FakeResultRepository());
            Assert.Throws<InvalidOperationException>(() => controller.BeginPlaying());
        }

        [Test]
        public void PushInput_ExactTime_GivesPerfect()
        {
            var noteRepo = new FakeNoteRepository();
            noteRepo.Notes.AddRange(DemoNotes());
            var controller = new SessionController(noteRepo, new FakeResultRepository());

            controller.SelectTrack(5);
            controller.BeginPlaying();

            // Нота 1 в 1.0 с → ожидаемое 1000 мс; точное попадание → Perfect.
            HitEvent hit = controller.PushInput(new InputEvent(1000.0, 60));

            Assert.IsNotNull(hit);
            Assert.AreEqual(Judgement.Perfect, hit.Judgement);
            Assert.AreEqual(0, hit.DeltaMs);
        }

        [Test]
        public void PushInput_OutsideWindow_ReturnsNull()
        {
            var noteRepo = new FakeNoteRepository();
            noteRepo.Notes.AddRange(DemoNotes());
            var controller = new SessionController(noteRepo, new FakeResultRepository());

            controller.SelectTrack(5);
            controller.BeginPlaying();

            // +300 мс от ожидаемого (> BadMs 200) — ноты в окне нет, лишнее нажатие.
            HitEvent hit = controller.PushInput(new InputEvent(1300.0, 60));

            Assert.IsNull(hit);
        }

        [Test]
        public void PushInput_BeforeBegin_Throws()
        {
            var controller = new SessionController(new FakeNoteRepository(), new FakeResultRepository());
            controller.SelectTrack(5); // Selecting, ещё не Playing

            Assert.Throws<InvalidOperationException>(() => controller.PushInput(new InputEvent(0.0, 60)));
        }

        [Test]
        public void EndPlaying_SavesResultAndHits_AndReturnsToIdle()
        {
            var noteRepo = new FakeNoteRepository();
            noteRepo.Notes.AddRange(DemoNotes());
            var resultRepo = new FakeResultRepository();
            var controller = new SessionController(noteRepo, resultRepo);

            controller.SelectTrack(5);
            controller.BeginPlaying();
            controller.PushInput(new InputEvent(1000.0, 60));
            controller.PushInput(new InputEvent(2000.0, 62));
            controller.PushInput(new InputEvent(3000.0, 64));

            SessionEvaluation eval = controller.EndPlaying(userId: 7, playedAt: When);

            // FSM завершён.
            Assert.AreEqual(GameSessionState.Idle, controller.State);
            Assert.IsNull(controller.SelectedTrackId);

            // Результат сохранён ровно один раз.
            Assert.AreEqual(1, resultRepo.Saved.Count);
            Assert.AreEqual(7, resultRepo.Saved[0].UserId);
            Assert.AreEqual(3, eval.Result.Score300);

            // Нажатия сохранены по одному на ноту и связаны с результатом через ResultId.
            Assert.AreEqual(3, resultRepo.SavedHits.Count);
            Assert.IsTrue(resultRepo.SavedHits.All(h => h.ResultId == resultRepo.Saved[0].ResultId));
        }

        [Test]
        public void Realtime_EquivalentTo_BatchRun()
        {
            var inputs = new[]
            {
                new InputEvent(1000.0, 60),  // +0 мс  → Perfect по ноте1
                new InputEvent(2080.0, 62),  // +80 мс → Good по ноте2
                new InputEvent(3300.0, 64)   // +300 мс (вне окна 200) → нота3 остаётся Miss
            };

            // --- Пакетный путь ---
            var batchNotes = new FakeNoteRepository();
            batchNotes.Notes.AddRange(DemoNotes());
            var batchResults = new FakeResultRepository();
            var batch = new SessionController(batchNotes, batchResults);
            batch.SelectTrack(5);
            SessionEvaluation batchEval = batch.PlaySelectedTrack(99, inputs, When);

            // --- Realtime путь (те же входы, в порядке времени) ---
            var rtNotes = new FakeNoteRepository();
            rtNotes.Notes.AddRange(DemoNotes());
            var rtResults = new FakeResultRepository();
            var rt = new SessionController(rtNotes, rtResults);
            rt.SelectTrack(5);
            rt.BeginPlaying();
            foreach (InputEvent i in inputs.OrderBy(i => i.TimeMs))
                rt.PushInput(i);
            SessionEvaluation rtEval = rt.EndPlaying(99, When);

            // Итоги должны полностью совпасть — один и тот же GameEngine-алгоритм.
            Assert.AreEqual(batchEval.Result.Score300, rtEval.Result.Score300);
            Assert.AreEqual(batchEval.Result.Score100, rtEval.Result.Score100);
            Assert.AreEqual(batchEval.Result.Score50, rtEval.Result.Score50);
            Assert.AreEqual(batchEval.Result.MissCount, rtEval.Result.MissCount);
            Assert.AreEqual(batchEval.Result.MaxCombo, rtEval.Result.MaxCombo);
            Assert.AreEqual(batchEval.Result.AccuracyPercent, rtEval.Result.AccuracyPercent, 1e-9);
            Assert.AreEqual(batchEval.HitEvents.Count, rtEval.HitEvents.Count);
        }

        // --- Фейки репозиториев (реализуют контракты Domain) ---

        private sealed class FakeNoteRepository : INoteRepository
        {
            public List<Note> Notes { get; } = new List<Note>();

            public IEnumerable<Note> GetNotesForTrack(int trackId) =>
                Notes.FindAll(n => n.TrackId == trackId);

            // Фейк без переопределений аппликатуры — userId игнорируется.
            public IEnumerable<Note> GetNotesForTrack(int trackId, int userId) =>
                GetNotesForTrack(trackId);

            public Note GetNoteById(int noteId) => Notes.Find(n => n.NoteId == noteId);
            public void AddNote(Note note) => Notes.Add(note);
            public void BulkInsertNotes(IEnumerable<Note> notes) => Notes.AddRange(notes);
            public void UpdateNoteFinger(int noteId, int finger, string hand) { }
        }

        private sealed class FakeResultRepository : IResultRepository
        {
            private int _nextResultId = 41;

            public List<GameResult> Saved { get; } = new List<GameResult>();
            public List<HitEvent> SavedHits { get; } = new List<HitEvent>();

            // Имитируем last_insert_rowid: проставляем ResultId, как реальный SQLite-репозиторий.
            public void SaveGameResult(GameResult result)
            {
                result.ResultId = ++_nextResultId;
                Saved.Add(result);
            }

            public void SaveHitEvents(IEnumerable<HitEvent> hits) => SavedHits.AddRange(hits);
            public IEnumerable<GameResult> GetUserHistory(int userId) => Saved;
            public float GetAverageAccuracy(int userId, int trackId) => 0f;
            public Dictionary<string, int> GetWeakSpots(int userId) => new Dictionary<string, int>();
        }
    }
}
