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

        // §2.3: рука/палец — переопределение пользователя, иначе значения из Notes (БД активна — COALESCE в SQL).
        public IEnumerable<Note> GetNotesForTrack(int trackId, int userId) =>
            _conn.Query<Note>(
                @"SELECT n.note_id AS NoteId, n.track_id AS TrackId, n.note_index AS NoteIndex,
                         n.midi_number AS MidiNumber, n.start_time AS StartTime, n.duration AS Duration,
                         COALESCE(ufa.assigned_hand,   n.hand)   AS Hand,
                         COALESCE(ufa.assigned_finger, n.finger) AS Finger
                  FROM Notes n
                  LEFT JOIN UserFingerAssignments ufa
                         ON ufa.note_id = n.note_id AND ufa.user_id = ?
                  WHERE n.track_id = ?
                  ORDER BY n.start_time;",
                userId, trackId);

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
