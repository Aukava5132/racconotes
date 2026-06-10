using System;
using System.Collections.Generic;
using NUnit.Framework;
using Racconotes.Application;
using Racconotes.Application.Engine;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;
using Racconotes.Domain.Repositories;

namespace Racconotes.Tests
{
    /// <summary>
    /// SessionController с in-memory фейками репозиториев: полный проход FSM §1.3
    /// и сохранение корректного GameResult через абстракцию IResultRepository (DIP).
    /// </summary>
    public class SessionControllerTests
    {
        private static readonly DateTime When = new DateTime(2026, 6, 10);

        [Test]
        public void PlaySelectedTrack_RunsFsm_AndSavesResult()
        {
            var noteRepo = new FakeNoteRepository
            {
                Notes =
                {
                    new Note { NoteId = 1, TrackId = 5, MidiNumber = 60, StartTime = 1.0 },
                    new Note { NoteId = 2, TrackId = 5, MidiNumber = 62, StartTime = 2.0 }
                }
            };
            var resultRepo = new FakeResultRepository();
            var controller = new SessionController(noteRepo, resultRepo);

            controller.SelectTrack(5);
            Assert.AreEqual(GameSessionState.Selecting, controller.State);

            var inputs = new[]
            {
                new InputEvent(1000.0, 60), // Perfect по ноте1
                new InputEvent(2000.0, 62)  // Perfect по ноте2
            };
            SessionEvaluation eval = controller.PlaySelectedTrack(userId: 7, inputs: inputs, playedAt: When);

            // FSM вернулся в Idle.
            Assert.AreEqual(GameSessionState.Idle, controller.State);
            Assert.IsNull(controller.SelectedTrackId);

            // Результат сохранён ровно один раз и совпадает с возвращённым.
            Assert.AreEqual(1, resultRepo.Saved.Count);
            GameResult saved = resultRepo.Saved[0];
            Assert.AreSame(eval.Result, saved);
            Assert.AreEqual(7, saved.UserId);
            Assert.AreEqual(5, saved.TrackId);
            Assert.AreEqual(2, saved.Score300);
            Assert.AreEqual(100.0, saved.AccuracyPercent, 1e-9);
        }

        [Test]
        public void PlaySelectedTrack_WithoutSelect_Throws()
        {
            var controller = new SessionController(new FakeNoteRepository(), new FakeResultRepository());
            Assert.Throws<InvalidOperationException>(
                () => controller.PlaySelectedTrack(1, Array.Empty<InputEvent>(), When));
        }

        // --- Фейки репозиториев (реализуют контракты Domain) ---

        private sealed class FakeNoteRepository : INoteRepository
        {
            public List<Note> Notes { get; } = new List<Note>();

            public IEnumerable<Note> GetNotesForTrack(int trackId) =>
                Notes.FindAll(n => n.TrackId == trackId);

            public Note GetNoteById(int noteId) => Notes.Find(n => n.NoteId == noteId);
            public void AddNote(Note note) => Notes.Add(note);
            public void BulkInsertNotes(IEnumerable<Note> notes) => Notes.AddRange(notes);
            public void UpdateNoteFinger(int noteId, int finger, string hand) { }
        }

        private sealed class FakeResultRepository : IResultRepository
        {
            public List<GameResult> Saved { get; } = new List<GameResult>();

            public void SaveGameResult(GameResult result) => Saved.Add(result);
            public void SaveHitEvents(IEnumerable<HitEvent> hits) { } // не проверяется в этом тесте
            public IEnumerable<GameResult> GetUserHistory(int userId) => Saved;
            public float GetAverageAccuracy(int userId, int trackId) => 0f;
            public Dictionary<string, int> GetWeakSpots(int userId) => new Dictionary<string, int>();
        }
    }
}
