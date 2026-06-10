using System.Collections.Generic;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Repositories;
using SQLite;

namespace Racconotes.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-реализация <see cref="INoteRepository"/>. Доменный <see cref="Note"/> — POCO без
    /// атрибутов, поэтому чтение использует явные AS-алиасы (snake_case колонка → PascalCase свойство;
    /// сопоставление sqlite-net регистронезависимо, но 'note_id' != 'noteid' — алиас обязателен).
    /// </summary>
    public sealed class SqliteNoteRepository : INoteRepository
    {
        // Проекция колонок Notes на свойства Note.
        private const string Select =
            @"SELECT note_id AS NoteId, track_id AS TrackId, note_index AS NoteIndex,
                     midi_number AS MidiNumber, start_time AS StartTime, duration AS Duration,
                     hand AS Hand, finger AS Finger
              FROM Notes";

        private const string Insert =
            @"INSERT INTO Notes (track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (?,?,?,?,?,?,?);";

        private readonly SQLiteConnection _conn;

        public SqliteNoteRepository(SQLiteConnection conn) => _conn = conn;

        public IEnumerable<Note> GetNotesForTrack(int trackId) =>
            _conn.Query<Note>(Select + " WHERE track_id = ? ORDER BY start_time;", trackId);

        public Note GetNoteById(int noteId)
        {
            List<Note> rows = _conn.Query<Note>(Select + " WHERE note_id = ?;", noteId);
            return rows.Count > 0 ? rows[0] : null;
        }

        public void AddNote(Note note)
        {
            _conn.Execute(Insert,
                note.TrackId, note.NoteIndex, note.MidiNumber, note.StartTime,
                note.Duration, note.Hand, note.Finger);
            note.NoteId = (int)_conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
        }

        public void BulkInsertNotes(IEnumerable<Note> notes)
        {
            _conn.RunInTransaction(() =>
            {
                foreach (Note note in notes)
                {
                    _conn.Execute(Insert,
                        note.TrackId, note.NoteIndex, note.MidiNumber, note.StartTime,
                        note.Duration, note.Hand, note.Finger);
                    note.NoteId = (int)_conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                }
            });
        }

        public void UpdateNoteFinger(int noteId, int finger, string hand) =>
            _conn.Execute("UPDATE Notes SET finger = ?, hand = ? WHERE note_id = ?;", finger, hand, noteId);
    }
}
