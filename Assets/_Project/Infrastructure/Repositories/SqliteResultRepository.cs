using System;
using System.Collections.Generic;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Repositories;
using SQLite;

namespace Racconotes.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-реализация <see cref="IResultRepository"/>: запись результатов/нажатий и агрегаты.
    /// played_at пишется явным DateTime-параметром (тики), а не DEFAULT CURRENT_TIMESTAMP — иначе
    /// чтение DateTime из тиков сломается (см. <see cref="Database.DatabaseConnectionFactory"/>).
    /// judgement пишется строкой ToLowerInvariant() (CHECK в схеме страхует), читается обратно как
    /// enum автоматически (Enum.Parse в sqlite-net).
    /// </summary>
    public sealed class SqliteResultRepository : IResultRepository
    {
        private const string SelectResult =
            @"SELECT result_id AS ResultId, user_id AS UserId, track_id AS TrackId,
                     played_at AS PlayedAt, accuracy_percent AS AccuracyPercent,
                     score_300 AS Score300, score_100 AS Score100, score_50 AS Score50,
                     miss_count AS MissCount, max_combo AS MaxCombo
              FROM GameResults";

        private readonly SQLiteConnection _conn;

        public SqliteResultRepository(SQLiteConnection conn) => _conn = conn;

        public void SaveGameResult(GameResult result)
        {
            _conn.Execute(
                @"INSERT INTO GameResults (user_id, track_id, played_at, accuracy_percent,
                    score_300, score_100, score_50, miss_count, max_combo)
                  VALUES (?,?,?,?,?,?,?,?,?);",
                result.UserId, result.TrackId, result.PlayedAt, result.AccuracyPercent,
                result.Score300, result.Score100, result.Score50, result.MissCount, result.MaxCombo);
            result.ResultId = (int)_conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
        }

        public void SaveHitEvents(IEnumerable<HitEvent> hits)
        {
            _conn.RunInTransaction(() =>
            {
                foreach (HitEvent hit in hits)
                {
                    _conn.Execute(
                        @"INSERT INTO HitEvents (result_id, note_id, expected_time, actual_time,
                            delta_ms, judgement, finger_used)
                          VALUES (?,?,?,?,?,?,?);",
                        hit.ResultId, hit.NoteId, hit.ExpectedTime, hit.ActualTime,
                        hit.DeltaMs, hit.Judgement.ToString().ToLowerInvariant(), hit.FingerUsed);
                }
            });
        }

        public IEnumerable<GameResult> GetUserHistory(int userId) =>
            _conn.Query<GameResult>(SelectResult + " WHERE user_id = ? ORDER BY played_at;", userId);

        public float GetAverageAccuracy(int userId, int trackId) =>
            (float)_conn.ExecuteScalar<double>(
                "SELECT COALESCE(AVG(accuracy_percent), 0) FROM GameResults WHERE user_id = ? AND track_id = ?;",
                userId, trackId);

        public Dictionary<string, int> GetWeakSpots(int userId)
        {
            // Запрос 2 (§2.4), форма "midi/hand/finger" → число промахов.
            List<WeakSpotRow> rows = _conn.Query<WeakSpotRow>(
                @"SELECT n.midi_number AS MidiNumber, n.hand AS Hand, n.finger AS Finger, COUNT(*) AS MissCount
                  FROM HitEvents h
                  JOIN Notes n        ON h.note_id = n.note_id
                  JOIN GameResults gr ON h.result_id = gr.result_id
                  WHERE h.judgement = 'miss' AND gr.user_id = ?
                  GROUP BY n.midi_number, n.hand, n.finger
                  ORDER BY MissCount DESC, n.midi_number;", userId);

            var result = new Dictionary<string, int>(rows.Count);
            foreach (WeakSpotRow r in rows)
                result[$"{r.MidiNumber}/{r.Hand}/{r.Finger}"] = r.MissCount;
            return result;
        }

        // Промежуточная строка-результат для словаря слабых мест.
        private sealed class WeakSpotRow
        {
            public int MidiNumber { get; set; }
            public string Hand { get; set; }
            public int Finger { get; set; }
            public int MissCount { get; set; }
        }
    }
}
