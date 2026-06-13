using System.Collections.Generic;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Repositories;
using SQLite;

namespace Racconotes.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-реализация <see cref="IUserFingerAssignmentRepository"/> (§2.3). POCO без атрибутов →
    /// читаем через AS-алиасы PascalCase (snake_case колонка → свойство). UPSERT = UPDATE→если
    /// 0 строк INSERT (без зависимости от синтаксиса ON CONFLICT; UNIQUE по user+track+note).
    /// </summary>
    public sealed class SqliteUserFingerAssignmentRepository : IUserFingerAssignmentRepository
    {
        private const string Select =
            @"SELECT assignment_id AS AssignmentId, user_id AS UserId, track_id AS TrackId, note_id AS NoteId,
                     assigned_hand AS AssignedHand, assigned_finger AS AssignedFinger, is_edited AS IsEdited
              FROM UserFingerAssignments";

        private readonly SQLiteConnection _conn;

        public SqliteUserFingerAssignmentRepository(SQLiteConnection conn) => _conn = conn;

        public IEnumerable<UserFingerAssignment> GetForTrack(int userId, int trackId) =>
            _conn.Query<UserFingerAssignment>(
                Select + " WHERE user_id = ? AND track_id = ? ORDER BY note_id;", userId, trackId);

        public void Upsert(UserFingerAssignment a)
        {
            int updated = _conn.Execute(
                @"UPDATE UserFingerAssignments SET assigned_hand = ?, assigned_finger = ?, is_edited = 1
                  WHERE user_id = ? AND track_id = ? AND note_id = ?;",
                a.AssignedHand, a.AssignedFinger, a.UserId, a.TrackId, a.NoteId);

            if (updated == 0)
                _conn.Execute(
                    @"INSERT INTO UserFingerAssignments
                        (user_id, track_id, note_id, assigned_hand, assigned_finger, is_edited)
                      VALUES (?,?,?,?,?,1);",
                    a.UserId, a.TrackId, a.NoteId, a.AssignedHand, a.AssignedFinger);
        }

        public void Reset(int userId, int trackId, int noteId) =>
            _conn.Execute(
                "DELETE FROM UserFingerAssignments WHERE user_id = ? AND track_id = ? AND note_id = ?;",
                userId, trackId, noteId);
    }
}
