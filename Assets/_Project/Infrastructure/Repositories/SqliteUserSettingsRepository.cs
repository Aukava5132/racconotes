using System.Collections.Generic;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Repositories;
using SQLite;

namespace Racconotes.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-реализация <see cref="IUserSettingsRepository"/>: чтение и UPSERT по UserSettings.
    /// POCO без атрибутов → читаем через AS-алиасы PascalCase (snake_case не матчится напрямую).
    /// Сохранение: UPDATE, а если строки нет (0 затронутых) — INSERT (UPSERT без зависимости от
    /// синтаксиса ON CONFLICT).
    /// </summary>
    public sealed class SqliteUserSettingsRepository : IUserSettingsRepository
    {
        private const string Select =
            @"SELECT user_id AS UserId, visual_offset_ms AS VisualOffsetMs, audio_offset_ms AS AudioOffsetMs,
                     min_bpm_filter AS MinBpmFilter, max_bpm_filter AS MaxBpmFilter, preferred_hand AS PreferredHand,
                     difficulty_filter_min AS DifficultyFilterMin, difficulty_filter_max AS DifficultyFilterMax,
                     key_label_mode AS KeyLabelMode, note_label_mode AS NoteLabelMode
              FROM UserSettings";

        private readonly SQLiteConnection _conn;

        public SqliteUserSettingsRepository(SQLiteConnection conn) => _conn = conn;

        public UserSettings GetSettings(int userId)
        {
            List<UserSettings> rows = _conn.Query<UserSettings>(Select + " WHERE user_id = ?;", userId);
            return rows.Count > 0 ? rows[0] : null;
        }

        public void SaveSettings(UserSettings s)
        {
            string keyMode = s.KeyLabelMode ?? "off";
            string noteMode = s.NoteLabelMode ?? "off";

            int updated = _conn.Execute(
                @"UPDATE UserSettings SET
                    visual_offset_ms = ?, audio_offset_ms = ?, min_bpm_filter = ?, max_bpm_filter = ?,
                    preferred_hand = ?, difficulty_filter_min = ?, difficulty_filter_max = ?,
                    key_label_mode = ?, note_label_mode = ?
                  WHERE user_id = ?;",
                s.VisualOffsetMs, s.AudioOffsetMs, s.MinBpmFilter, s.MaxBpmFilter,
                s.PreferredHand, s.DifficultyFilterMin, s.DifficultyFilterMax,
                keyMode, noteMode, s.UserId);

            if (updated == 0)
                _conn.Execute(
                    @"INSERT INTO UserSettings
                        (user_id, visual_offset_ms, audio_offset_ms, min_bpm_filter, max_bpm_filter,
                         preferred_hand, difficulty_filter_min, difficulty_filter_max, key_label_mode, note_label_mode)
                      VALUES (?,?,?,?,?,?,?,?,?,?);",
                    s.UserId, s.VisualOffsetMs, s.AudioOffsetMs, s.MinBpmFilter, s.MaxBpmFilter,
                    s.PreferredHand, s.DifficultyFilterMin, s.DifficultyFilterMax, keyMode, noteMode);
        }
    }
}
