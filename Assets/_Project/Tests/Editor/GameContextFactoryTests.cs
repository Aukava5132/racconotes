using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Racconotes.Application;
using Racconotes.Application.Engine;
using Racconotes.Composition;
using Racconotes.Domain.Entities;
using Racconotes.Infrastructure.Database;
using SQLite;

namespace Racconotes.Tests
{
    /// <summary>
    /// Композиционный корень рантайма: <see cref="GameContextFactory.Build"/> собирает рабочий граф
    /// (репозитории + сервисы) на одном соединении, а сквозная сессия читает ноты из БД и пишет
    /// результат обратно — доказательство «БД как активный компонент» без запуска Unity Play Mode.
    /// </summary>
    public class GameContextFactoryTests
    {
        private const int SeedUserId = 1;
        private const int SeedTrackId = 1; // «Ода к радости», 6 нот (см. SqlSeed)

        private SQLiteConnection _conn;

        [SetUp]
        public void SetUp()
        {
            _conn = DatabaseConnectionFactory.OpenInMemory();
            DatabaseInitializer.EnsureCreated(_conn, seed: true);
        }

        [TearDown]
        public void TearDown() => _conn?.Dispose();

        [Test]
        public void Build_PopulatesAllDependencies()
        {
            GameContext ctx = GameContextFactory.Build(_conn);

            Assert.IsNotNull(ctx.TrackRepository);
            Assert.IsNotNull(ctx.NoteRepository);
            Assert.IsNotNull(ctx.ResultRepository);
            Assert.IsNotNull(ctx.StatsQueries);
            Assert.IsNotNull(ctx.UserSettingsRepository);
            Assert.IsNotNull(ctx.Session);
            Assert.IsNotNull(ctx.MidiImport);
            Assert.IsNotNull(ctx.StatsAnalyzer);
        }

        [Test]
        public void Build_WithSeed_HasTracks()
        {
            GameContext ctx = GameContextFactory.Build(_conn);

            Assert.IsTrue(ctx.TrackRepository.GetAllTracks().Any(),
                "Свежая БД с seed:true должна содержать демо-треки.");
        }

        [Test]
        public void Session_PlaySelectedTrack_EndToEnd()
        {
            GameContext ctx = GameContextFactory.Build(_conn);

            // Идеальные нажатия: момент в мс = StartTime(сек) × 1000, та же высота — все Perfect.
            List<Note> notes = ctx.NoteRepository.GetNotesForTrack(SeedTrackId).ToList();
            Assert.IsNotEmpty(notes, "У seed-трека должны быть ноты.");

            List<InputEvent> inputs = notes
                .Select(n => new InputEvent(n.StartTime * 1000.0, n.MidiNumber, n.Finger))
                .ToList();

            int historyBefore = ctx.ResultRepository.GetUserHistory(SeedUserId).Count();

            ctx.Session.SelectTrack(SeedTrackId);
            SessionEvaluation eval = ctx.Session.PlaySelectedTrack(
                SeedUserId, inputs, new DateTime(2026, 6, 10, 12, 0, 0));

            Assert.IsNotNull(eval);
            Assert.AreEqual(notes.Count, eval.HitEvents.Count, "По одному HitEvent на каждую ноту.");
            Assert.AreEqual(100.0, eval.Result.AccuracyPercent, 1e-6, "Идеальные нажатия → 100%.");

            // Результат записан в БД (БД — активный компонент).
            Assert.Greater(eval.Result.ResultId, 0);
            Assert.AreEqual(historyBefore + 1, ctx.ResultRepository.GetUserHistory(SeedUserId).Count());
        }
    }
}
