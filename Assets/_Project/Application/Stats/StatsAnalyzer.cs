using System.Collections.Generic;
using System.Linq;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;
using Racconotes.Domain.Stats;

namespace Racconotes.Application.Stats
{
    /// <summary>
    /// Аналитика поверх коллекций результатов и нажатий — in-memory эквиваленты сложных
    /// SQL-запросов §2.4. Логика живёт в Application, а не только в SQL: на Этапе 2 те же
    /// вычисления можно выполнять запросами в БД, но бизнес-правило определено здесь.
    /// Чистые методы без состояния.
    /// </summary>
    public sealed class StatsAnalyzer
    {
        /// <summary>
        /// Запрос 2: самые сложные ноты — где чаще всего Miss. Группировка по высоте/руке/пальцу,
        /// сортировка по числу промахов по убыванию, ограничение <paramref name="top"/>.
        /// </summary>
        public IReadOnlyList<WeakSpot> GetWeakSpots(
            IEnumerable<HitEvent> hits, IEnumerable<Note> notes, int top = 10)
        {
            Dictionary<int, Note> byId = notes.ToDictionary(n => n.NoteId);

            return hits
                .Where(h => h.Judgement == Judgement.Miss && byId.ContainsKey(h.NoteId))
                .Select(h => byId[h.NoteId])
                .GroupBy(n => new { n.MidiNumber, n.Hand, n.Finger })
                .Select(g => new WeakSpot
                {
                    MidiNumber = g.Key.MidiNumber,
                    Hand = g.Key.Hand,
                    Finger = g.Key.Finger,
                    MissCount = g.Count()
                })
                .OrderByDescending(w => w.MissCount)
                .ThenBy(w => w.MidiNumber)
                .Take(top)
                .ToList();
        }

        /// <summary>
        /// Запрос 4: средняя задержка по руке/пальцу для засчитанных, но неидеальных нажатий
        /// (Good/Bad). Положительное среднее — опаздывает, отрицательное — торопится.
        /// </summary>
        public IReadOnlyList<LatencyByFinger> GetAverageLatency(
            IEnumerable<HitEvent> hits, IEnumerable<Note> notes)
        {
            Dictionary<int, Note> byId = notes.ToDictionary(n => n.NoteId);

            return hits
                .Where(h => (h.Judgement == Judgement.Good || h.Judgement == Judgement.Bad)
                            && byId.ContainsKey(h.NoteId))
                .Select(h => new { Note = byId[h.NoteId], h.DeltaMs })
                .GroupBy(x => new { x.Note.Hand, x.Note.Finger })
                .Select(g => new LatencyByFinger
                {
                    Hand = g.Key.Hand,
                    Finger = g.Key.Finger,
                    AvgDeltaMs = g.Average(x => (double)x.DeltaMs),
                    HitCount = g.Count()
                })
                .OrderByDescending(l => l.AvgDeltaMs)
                .ToList();
        }

        /// <summary>
        /// Запрос 1: прогресс точности по треку во времени. Improvement каждой точки —
        /// разница с предыдущей (аналог LAG OVER (ORDER BY played_at)).
        /// </summary>
        public IReadOnlyList<AccuracyPoint> GetAccuracyProgress(
            IEnumerable<GameResult> results, int trackId)
        {
            List<GameResult> ordered = results
                .Where(r => r.TrackId == trackId)
                .OrderBy(r => r.PlayedAt)
                .ToList();

            var points = new List<AccuracyPoint>(ordered.Count);
            double? prev = null;
            foreach (GameResult r in ordered)
            {
                points.Add(new AccuracyPoint
                {
                    PlayedAt = r.PlayedAt,
                    Accuracy = r.AccuracyPercent,
                    PrevAccuracy = prev,
                    Improvement = prev.HasValue ? r.AccuracyPercent - prev.Value : (double?)null
                });
                prev = r.AccuracyPercent;
            }
            return points;
        }
    }
}