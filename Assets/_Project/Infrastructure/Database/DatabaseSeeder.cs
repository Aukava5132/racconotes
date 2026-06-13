using System;
using System.Collections.Generic;
using System.Linq;
using Racconotes.Domain.Entities;
using Racconotes.Infrastructure.Repositories;
using SQLite;

namespace Racconotes.Infrastructure.Database
{
    /// <summary>
    /// Наполнение демо-БД. Пользователь/настройки — из <see cref="SqlSeed"/>; встроенные треки и ноты —
    /// из <see cref="BuiltInTrackCatalog"/> через репозитории (срабатывает триггер update_track_range);
    /// игровые результаты и нажатия — программно с датами-ТИКАМИ (played_at читается как DateTime,
    /// поэтому строковый CURRENT_TIMESTAMP здесь нельзя). Данные подобраны так, чтобы все 4 запроса
    /// §2.4 возвращали непустой осмысленный результат.
    ///
    /// <see cref="SyncBuiltInCatalog"/> — идемпотентная синхронизация встроенного каталога для уже
    /// созданной БД: удаляет прежние («стандартные») треки и добавляет отсутствующие, НЕ трогая
    /// импортированные пользователем треки.
    /// </summary>
    public static class DatabaseSeeder
    {
        public static void Seed(SQLiteConnection conn)
        {
            foreach (string statement in SqlSeed.Statements)
                conn.Execute(statement);

            // Вставляем весь встроенный каталог; на пустой БД первый трек получает track_id=1.
            var trackRepo = new SqliteTrackRepository(conn);
            var noteRepo = new SqliteNoteRepository(conn);

            int firstTrackId = 0;
            IReadOnlyList<Note> firstNotes = null;
            foreach (BuiltInTrackCatalog.BuiltInTrack t in BuiltInTrackCatalog.Tracks)
            {
                (int trackId, List<Note> notes) = InsertTrack(trackRepo, noteRepo, t);
                if (firstTrackId == 0)
                {
                    firstTrackId = trackId;
                    firstNotes = notes;
                }
            }

            // Демо-прохождения биндим на реальные id первого трека и его нот (а не на хардкод 1..6).
            SeedDemoPlaythroughs(conn, firstTrackId, firstNotes);
        }

        /// <summary>
        /// Привести встроенный каталог уже созданной БД к актуальному: удалить «стандартные» треки
        /// (по заголовку, каскадно с историей) и добавить отсутствующие из каталога. Импортированные
        /// треки (с другими заголовками) не затрагиваются. Идемпотентно: на свежей seed-БД это no-op.
        /// </summary>
        public static void SyncBuiltInCatalog(SQLiteConnection conn)
        {
            var trackRepo = new SqliteTrackRepository(conn);
            var noteRepo = new SqliteNoteRepository(conn);

            // 1) Удалить прежние встроенные треки по заголовку (DeleteTrack чистит зависимую историю).
            foreach (string legacy in BuiltInTrackCatalog.LegacyTitles)
                foreach (int id in TrackIdsByTitle(conn, legacy))
                    trackRepo.DeleteTrack(id);

            // 2) Добавить отсутствующие треки каталога (сопоставление по заголовку).
            foreach (BuiltInTrackCatalog.BuiltInTrack t in BuiltInTrackCatalog.Tracks)
            {
                if (TrackIdsByTitle(conn, t.Track.Title).Count > 0) continue; // уже есть — пропускаем
                InsertTrack(trackRepo, noteRepo, t);
            }
        }

        // ---- Вставка одного трека каталога (клонируем, чтобы не мутировать общие статические данные) ----

        private static (int trackId, List<Note> notes) InsertTrack(
            SqliteTrackRepository trackRepo, SqliteNoteRepository noteRepo, BuiltInTrackCatalog.BuiltInTrack t)
        {
            int trackId = trackRepo.AddTrack(CloneTrack(t.Track));
            List<Note> notes = CloneNotes(t.Notes, trackId);
            noteRepo.BulkInsertNotes(notes); // проставит NoteId и активирует триггер диапазона
            return (trackId, notes);
        }

        private static MidiTrack CloneTrack(MidiTrack t) => new MidiTrack
        {
            Filename = t.Filename, Title = t.Title, Composer = t.Composer, Bpm = t.Bpm,
            Tonality = t.Tonality, Difficulty = t.Difficulty, NoteDensity = t.NoteDensity
        };

        private static List<Note> CloneNotes(IEnumerable<Note> notes, int trackId) =>
            notes.Select(n => new Note
            {
                TrackId = trackId, NoteIndex = n.NoteIndex, MidiNumber = n.MidiNumber,
                StartTime = n.StartTime, Duration = n.Duration, Hand = n.Hand, Finger = n.Finger
            }).ToList();

        private static List<int> TrackIdsByTitle(SQLiteConnection conn, string title) =>
            conn.Query<IdRow>("SELECT track_id AS Id FROM MidiTracks WHERE title = ?;", title)
                .Select(r => r.Id).ToList();

        private sealed class IdRow { public int Id { get; set; } }

        // ---- Демо-прохождения для аналитики §2.4 ----

        private static void SeedDemoPlaythroughs(SQLiteConnection conn, int trackId, IReadOnlyList<Note> notes)
        {
            if (trackId == 0 || notes == null || notes.Count == 0) return;

            DateTime now = DateTime.UtcNow;

            // Три сессии user 1 по первому треку с растущей точностью — для Запроса 1 (прогресс, LAG/OVER).
            int r1 = InsertResult(conn, 1, trackId, now.AddDays(-6), 70, 3, 2, 0, 1, 4);
            InsertResult(conn, 1, trackId, now.AddDays(-3), 82, 4, 1, 1, 0, 6);
            int r3 = InsertResult(conn, 1, trackId, now.AddDays(-1), 91, 5, 1, 0, 0, 6);

            // Нажатия последней сессии — для Запросов 2 (miss) и 4 (задержка good/bad).
            // 3-я нота трека — слабое место (промах). Остальное: perfect/good/bad с дельтами.
            int n0 = NoteId(notes, 0);
            int n1 = NoteId(notes, 1);
            int n2 = NoteId(notes, 2);
            int n3 = NoteId(notes, 3);
            int n4 = NoteId(notes, 4);
            int n5 = NoteId(notes, 5);

            InsertHit(conn, r3, n0, "perfect", 0);
            InsertHit(conn, r3, n1, "perfect", 5);
            InsertHit(conn, r3, n2, "miss", 0);
            InsertHit(conn, r3, n3, "good", 70);   // опаздывает
            InsertHit(conn, r3, n4, "bad", 150);   // сильно опаздывает
            InsertHit(conn, r3, n5, "good", -60);  // торопится

            // Ещё один промах по той же ноте в ранней сессии — усиливаем слабое место.
            InsertHit(conn, r1, n2, "miss", 0);
        }

        // Безопасный доступ к id ноты по индексу (на случай короткого первого трека).
        private static int NoteId(IReadOnlyList<Note> notes, int index) =>
            notes[Math.Min(index, notes.Count - 1)].NoteId;

        private static int InsertResult(SQLiteConnection conn, int userId, int trackId, DateTime playedAt,
            double accuracy, int s300, int s100, int s50, int miss, int combo)
        {
            conn.Execute(
                @"INSERT INTO GameResults (user_id, track_id, played_at, accuracy_percent,
                    score_300, score_100, score_50, miss_count, max_combo)
                  VALUES (?,?,?,?,?,?,?,?,?);",
                userId, trackId, playedAt, accuracy, s300, s100, s50, miss, combo);
            return (int)conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
        }

        private static void InsertHit(SQLiteConnection conn, int resultId, int noteId, string judgement, int deltaMs)
        {
            conn.Execute(
                @"INSERT INTO HitEvents (result_id, note_id, expected_time, actual_time, delta_ms, judgement, finger_used)
                  VALUES (?,?,?,?,?,?,?);",
                resultId, noteId, 0.0, (double)deltaMs, deltaMs, judgement, 0);
        }
    }
}
