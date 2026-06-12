using System.Collections.Generic;
using Racconotes.Domain.Entities;

namespace Racconotes.Domain.Repositories
{
    /// <summary>
    /// Контракт доступа к нотам. Реализация (SQLiteRepo) живёт в слое Infrastructure.
    /// DIP: внутренние слои (Application) зависят от этой абстракции, а не от SQLite.
    /// Конкретную реализацию подставляет только слой Composition.
    /// </summary>
    public interface INoteRepository
    {
        IEnumerable<Note> GetNotesForTrack(int trackId);

        /// <summary>
        /// Ноты трека с применённой пользовательской аппликатурой (§2.3): рука/палец берутся из
        /// UserFingerAssignments при наличии переопределения, иначе из Notes (COALESCE).
        /// </summary>
        IEnumerable<Note> GetNotesForTrack(int trackId, int userId);

        Note GetNoteById(int noteId);
        void AddNote(Note note);
        void BulkInsertNotes(IEnumerable<Note> notes);            // для импорта MIDI
        void UpdateNoteFinger(int noteId, int finger, string hand);
    }
}
