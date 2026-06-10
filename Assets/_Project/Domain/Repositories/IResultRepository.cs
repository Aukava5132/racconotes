using System.Collections.Generic;
using Racconotes.Domain.Entities;

namespace Racconotes.Domain.Repositories
{
    /// <summary>
    /// Контракт хранения результатов и агрегатов истории. Реализация — в Infrastructure (Этап 2).
    /// </summary>
    public interface IResultRepository
    {
        /// <summary>
        /// Сохраняет результат сессии. Реализация проставляет <see cref="GameResult.ResultId"/>
        /// сгенерированным id, чтобы вызывающий мог связать с ним нажатия (<see cref="SaveHitEvents"/>).
        /// </summary>
        void SaveGameResult(GameResult result);

        /// <summary>
        /// Сохраняет нажатия сессии в таблицу HitEvents (§2.1). Вызывающий заранее проставляет
        /// <see cref="HitEvent.ResultId"/>. Источник данных для аналитики §2.4 (weak spots, задержка).
        /// </summary>
        void SaveHitEvents(IEnumerable<HitEvent> hits);

        IEnumerable<GameResult> GetUserHistory(int userId);
        float GetAverageAccuracy(int userId, int trackId);
        Dictionary<string, int> GetWeakSpots(int userId); // где чаще всего miss
    }
}
