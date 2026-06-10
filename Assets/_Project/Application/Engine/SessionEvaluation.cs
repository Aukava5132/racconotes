using System.Collections.Generic;
using Racconotes.Domain.Entities;

namespace Racconotes.Application.Engine
{
    /// <summary>
    /// Итог прогона сессии: по одному <see cref="HitEvent"/> на каждую ноту (в порядке трека)
    /// и агрегированный <see cref="GameResult"/>.
    /// </summary>
    public sealed class SessionEvaluation
    {
        public IReadOnlyList<HitEvent> HitEvents { get; }
        public GameResult Result { get; }

        public SessionEvaluation(IReadOnlyList<HitEvent> hitEvents, GameResult result)
        {
            HitEvents = hitEvents;
            Result = result;
        }
    }
}
