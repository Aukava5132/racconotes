using System.Collections.Generic;
using Racconotes.Domain.Entities;

namespace Racconotes.Domain.Repositories
{
    /// <summary>
    /// Контракт доступа к пользовательской аппликатуре (таблица UserFingerAssignments, §2.3).
    /// Делает таблицу активным компонентом: редактор пальцев читает и пишет переопределения,
    /// а загрузка нот применяет их (COALESCE поверх Notes). Реализация — в Infrastructure (DIP).
    /// </summary>
    public interface IUserFingerAssignmentRepository
    {
        /// <summary>Все переопределения пользователя для трека (порядок по note_id).</summary>
        IEnumerable<UserFingerAssignment> GetForTrack(int userId, int trackId);

        /// <summary>Вставить или обновить переопределение руки/пальца ноты (UNIQUE user+track+note).</summary>
        void Upsert(UserFingerAssignment assignment);

        /// <summary>Удалить переопределение — вернуть ноту к авто-аппликатуре из Notes.</summary>
        void Reset(int userId, int trackId, int noteId);
    }
}
