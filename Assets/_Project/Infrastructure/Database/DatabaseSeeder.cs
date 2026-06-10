using System;
using SQLite;

namespace Racconotes.Infrastructure.Database
{
    /// <summary>
    /// Наполнение демо-БД. Справочники (Users/MidiTracks/Notes) — из <see cref="SqlSeed"/>;
    /// игровые результаты и нажатия — программно с датами-ТИКАМИ (played_at читается как DateTime,
    /// поэтому строковый CURRENT_TIMESTAMP здесь нельзя). Данные подобраны так, чтобы все 4
    /// запроса §2.4 возвращали непустой осмысленный результат.
    /// </summary>
    public static class DatabaseSeeder
    {
        public static void Seed(SQLiteConnection conn)
        {
            foreach (string statement in SqlSeed.Statements)
                conn.Execute(statement);

            SeedDemoPlaythroughs(conn);
        }

        private static void SeedDemoPlaythroughs(SQLiteConnection conn)
        {
            DateTime now = DateTime.UtcNow;

            // Три сессии user 1 по треку 1 с растущей точностью — для Запроса 1 (прогресс, LAG/OVER).
            int r1 = InsertResult(conn, 1, 1, now.AddDays(-6), 70, 3, 2, 0, 1, 4);
            InsertResult(conn, 1, 1, now.AddDays(-3), 82, 4, 1, 1, 0, 6);
            int r3 = InsertResult(conn, 1, 1, now.AddDays(-1), 91, 5, 1, 0, 0, 6);

            // Нажатия последней сессии — для Запросов 2 (miss) и 4 (задержка good/bad).
            // Нота F4 (note_id 3) — слабое место (промах). Остальное: perfect/good/bad с дельтами.
            InsertHit(conn, r3, 1, "perfect", 0);
            InsertHit(conn, r3, 2, "perfect", 5);
            InsertHit(conn, r3, 3, "miss", 0);
            InsertHit(conn, r3, 4, "good", 70);   // опаздывает
            InsertHit(conn, r3, 5, "bad", 150);   // сильно опаздывает
            InsertHit(conn, r3, 6, "good", -60);  // торопится

            // Ещё один промах по F4 в ранней сессии — усиливаем слабое место.
            InsertHit(conn, r1, 3, "miss", 0);
        }

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
