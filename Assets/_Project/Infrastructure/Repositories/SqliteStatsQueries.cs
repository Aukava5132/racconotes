using System;
using System.Collections.Generic;
using Racconotes.Domain.Repositories;
using Racconotes.Domain.Stats;
using SQLite;

namespace Racconotes.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-реализация сложных аналитических запросов §2.4 (оконные функции, подзапросы,
    /// агрегаты) — «БД как активный компонент». In-memory зеркало этих расчётов —
    /// <c>Racconotes.Application.Stats.StatsAnalyzer</c>; на одинаковых данных результаты совпадают.
    /// </summary>
    public sealed class SqliteStatsQueries : IStatsQueries
    {
        private readonly SQLiteConnection _conn;

        public SqliteStatsQueries(SQLiteConnection conn) => _conn = conn;

        // Запрос 1: прогресс точности по треку во времени (LAG/OVER).
        public IReadOnlyList<AccuracyPoint> GetAccuracyProgress(int userId, int trackId) =>
            _conn.Query<AccuracyPoint>(
                @"SELECT r.played_at        AS PlayedAt,
                         r.accuracy_percent AS Accuracy,
                         LAG(r.accuracy_percent) OVER (PARTITION BY r.track_id ORDER BY r.played_at) AS PrevAccuracy,
                         (r.accuracy_percent -
                          LAG(r.accuracy_percent) OVER (PARTITION BY r.track_id ORDER BY r.played_at)) AS Improvement
                  FROM GameResults r
                  WHERE r.user_id = ? AND r.track_id = ?
                  ORDER BY r.played_at;", userId, trackId);

        // Запрос 2: самые сложные ноты — где чаще всего Miss (per-user через JOIN GameResults).
        public IReadOnlyList<WeakSpot> GetWeakSpots(int userId, int top = 10) =>
            _conn.Query<WeakSpot>(
                @"SELECT n.midi_number AS MidiNumber, n.hand AS Hand, n.finger AS Finger, COUNT(*) AS MissCount
                  FROM HitEvents h
                  JOIN Notes n        ON h.note_id = n.note_id
                  JOIN GameResults gr ON h.result_id = gr.result_id
                  WHERE h.judgement = 'miss' AND gr.user_id = ?
                  GROUP BY n.midi_number, n.hand, n.finger
                  ORDER BY MissCount DESC, n.midi_number
                  LIMIT ?;", userId, top);

        // Запрос 3: рекомендации треков (не игранные за 7 дней + по уровню).
        // Порог даты — тиками (played_at хранится тиками), а не date('now',...).
        public IReadOnlyList<TrackRecommendation> RecommendTracks(int userId)
        {
            DateTime sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            return _conn.Query<TrackRecommendation>(
                @"SELECT t.track_id AS TrackId, t.title AS Title, t.difficulty AS Difficulty, t.bpm AS Bpm
                  FROM MidiTracks t
                  WHERE t.track_id NOT IN (
                          SELECT track_id FROM GameResults WHERE user_id = ? AND played_at > ?
                      )
                    AND t.difficulty BETWEEN
                        (SELECT COALESCE(AVG(accuracy_percent) / 10, 5) FROM GameResults WHERE user_id = ?)
                        AND 10
                  ORDER BY t.difficulty;", userId, sevenDaysAgo, userId);
        }

        // Запрос 4: средняя задержка по руке/пальцу для засчитанных, но неидеальных нажатий.
        public IReadOnlyList<LatencyByFinger> GetAverageLatency(int userId) =>
            _conn.Query<LatencyByFinger>(
                @"SELECT n.hand AS Hand, n.finger AS Finger, AVG(h.delta_ms) AS AvgDeltaMs, COUNT(*) AS HitCount
                  FROM HitEvents h
                  JOIN Notes n        ON h.note_id = n.note_id
                  JOIN GameResults gr ON h.result_id = gr.result_id
                  WHERE h.judgement IN ('good','bad') AND gr.user_id = ?
                  GROUP BY n.hand, n.finger
                  ORDER BY AvgDeltaMs DESC;", userId);
    }
}
