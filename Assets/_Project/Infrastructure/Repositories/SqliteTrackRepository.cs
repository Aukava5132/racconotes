using System.Collections.Generic;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Repositories;
using SQLite;

namespace Racconotes.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-реализация <see cref="ITrackRepository"/>: CRUD по MidiTracks и фильтры (§1.4).
    /// Удаление трека каскадно удаляет его ноты (FK ON DELETE CASCADE, §2.1) — требует
    /// PRAGMA foreign_keys=ON (см. <see cref="Database.DatabaseConnectionFactory"/>).
    /// </summary>
    public sealed class SqliteTrackRepository : ITrackRepository
    {
        private const string Select =
            @"SELECT track_id AS TrackId, filename AS Filename, title AS Title, composer AS Composer,
                     bpm AS Bpm, tonality AS Tonality, difficulty AS Difficulty, note_density AS NoteDensity
              FROM MidiTracks";

        private readonly SQLiteConnection _conn;

        public SqliteTrackRepository(SQLiteConnection conn) => _conn = conn;

        public IEnumerable<MidiTrack> GetAllTracks() =>
            _conn.Query<MidiTrack>(Select + " ORDER BY track_id;");

        public MidiTrack GetTrackById(int id)
        {
            List<MidiTrack> rows = _conn.Query<MidiTrack>(Select + " WHERE track_id = ?;", id);
            return rows.Count > 0 ? rows[0] : null;
        }

        public int AddTrack(MidiTrack track)
        {
            _conn.Execute(
                @"INSERT INTO MidiTracks (filename, title, composer, bpm, tonality, difficulty, note_density)
                  VALUES (?,?,?,?,?,?,?);",
                track.Filename, track.Title, track.Composer, track.Bpm,
                track.Tonality, track.Difficulty, track.NoteDensity);
            track.TrackId = (int)_conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
            return track.TrackId;
        }

        public void UpdateTrack(MidiTrack track) =>
            _conn.Execute(
                @"UPDATE MidiTracks SET filename = ?, title = ?, composer = ?, bpm = ?,
                    tonality = ?, difficulty = ?, note_density = ? WHERE track_id = ?;",
                track.Filename, track.Title, track.Composer, track.Bpm,
                track.Tonality, track.Difficulty, track.NoteDensity, track.TrackId);

        /// <summary>
        /// Удаляет трек со всей зависимой историей. ON DELETE CASCADE в схеме есть только у Notes;
        /// у GameResults/HitEvents/FingerMapping/UserFingerAssignments/TrackTransposition ссылки на
        /// трек/ноты БЕЗ каскада, поэтому при наличии истории игр голый DELETE трека падал бы
        /// «FOREIGN KEY constraint failed». Чистим зависимые строки в порядке, не нарушающем FK,
        /// одной транзакцией; Notes удаляются каскадом при удалении трека.
        /// </summary>
        public void DeleteTrack(int id) => _conn.RunInTransaction(() =>
        {
            _conn.Execute("DELETE FROM HitEvents WHERE result_id IN (SELECT result_id FROM GameResults WHERE track_id = ?);", id);
            _conn.Execute("DELETE FROM HitEvents WHERE note_id IN (SELECT note_id FROM Notes WHERE track_id = ?);", id);
            _conn.Execute("DELETE FROM GameResults WHERE track_id = ?;", id);
            _conn.Execute("DELETE FROM FingerMapping WHERE track_id = ?;", id);
            _conn.Execute("DELETE FROM UserFingerAssignments WHERE track_id = ?;", id);
            _conn.Execute("DELETE FROM TrackTransposition WHERE track_id = ?;", id);
            _conn.Execute("DELETE FROM MidiTracks WHERE track_id = ?;", id); // Notes удалятся каскадом
        });

        public IEnumerable<MidiTrack> FilterByDifficulty(float min, float max) =>
            _conn.Query<MidiTrack>(
                Select + " WHERE difficulty BETWEEN ? AND ? ORDER BY difficulty;", min, max);

        public IEnumerable<MidiTrack> FilterByBpm(int minBpm, int maxBpm) =>
            _conn.Query<MidiTrack>(
                Select + " WHERE bpm BETWEEN ? AND ? ORDER BY bpm;", minBpm, maxBpm);
    }
}
