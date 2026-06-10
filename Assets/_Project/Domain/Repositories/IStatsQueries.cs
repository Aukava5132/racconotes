using System.Collections.Generic;
using Racconotes.Domain.Stats;

namespace Racconotes.Domain.Repositories
{
    /// <summary>
    /// Сложные аналитические запросы §2.4, выполняемые СУБД (реальный SQL: оконные функции,
    /// подзапросы, агрегаты) — в отличие от in-memory <c>StatsAnalyzer</c> в Application.
    /// Воплощает критерий курсовой «БД как активный компонент». Реализация — в Infrastructure.
    /// DIP: Application/Presentation зависят от этой абстракции, не от SQLite.
    /// </summary>
    public interface IStatsQueries
    {
        /// <summary>Запрос 1: прогресс точности по треку во времени (LAG/OVER).</summary>
        IReadOnlyList<AccuracyPoint> GetAccuracyProgress(int userId, int trackId);

        /// <summary>Запрос 2: самые сложные ноты — где чаще всего Miss.</summary>
        IReadOnlyList<WeakSpot> GetWeakSpots(int userId, int top = 10);

        /// <summary>Запрос 3: рекомендации треков (давно не играл + подходит по уровню).</summary>
        IReadOnlyList<TrackRecommendation> RecommendTracks(int userId);

        /// <summary>Запрос 4: средняя задержка по руке/пальцу (где опаздывает/торопится).</summary>
        IReadOnlyList<LatencyByFinger> GetAverageLatency(int userId);
    }
}
