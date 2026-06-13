namespace Racconotes.Infrastructure.Database
{
    /// <summary>
    /// DDL схемы БД (§2.1 таблицы, §2.5 индексы, §2.6 триггеры, §2.7 версия).
    /// Хранится как массив ОТДЕЛЬНЫХ стейтментов: триггеры содержат внутренние ';',
    /// поэтому наивный split по ';' недопустим — каждый элемент выполняется как есть.
    ///
    /// MidiTracks объединяет базовый §2.1 и колонки §2.2 ALTER
    /// (min_required_keys, recommended_octave_range, can_transpose) прямо в CREATE,
    /// иначе триггер update_track_range, который их пишет, упадёт.
    /// </summary>
    public static class SqlSchema
    {
        /// <summary>Версия схемы, пишется в SchemaVersion (§2.7).</summary>
        public const int Version = 1;

        public static readonly string[] Statements =
        {
            // ----- §2.1 Основные таблицы (3НФ) -----
            @"CREATE TABLE Users (
                user_id      INTEGER PRIMARY KEY AUTOINCREMENT,
                username     TEXT UNIQUE NOT NULL,
                created_at   DATETIME DEFAULT CURRENT_TIMESTAMP,
                total_score  REAL DEFAULT 0,
                games_played INTEGER DEFAULT 0
            );",

            @"CREATE TABLE MidiTracks (
                track_id     INTEGER PRIMARY KEY AUTOINCREMENT,
                filename     TEXT UNIQUE,
                title        TEXT NOT NULL,
                composer     TEXT,
                bpm          REAL NOT NULL,
                tonality     TEXT,
                difficulty   REAL,
                note_density REAL,
                min_required_keys        INTEGER,
                recommended_octave_range TEXT,
                can_transpose            BOOLEAN DEFAULT 1
            );",

            @"CREATE TABLE Notes (
                note_id     INTEGER PRIMARY KEY AUTOINCREMENT,
                track_id    INTEGER NOT NULL,
                note_index  INTEGER,
                midi_number INTEGER NOT NULL,
                start_time  REAL NOT NULL,
                duration    REAL,
                hand        TEXT CHECK(hand IN ('left','right')),
                finger      INTEGER CHECK(finger BETWEEN 1 AND 5),
                FOREIGN KEY (track_id) REFERENCES MidiTracks(track_id) ON DELETE CASCADE
            );",

            @"CREATE TABLE GameResults (
                result_id        INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id          INTEGER NOT NULL,
                track_id         INTEGER NOT NULL,
                played_at        DATETIME DEFAULT CURRENT_TIMESTAMP,
                accuracy_percent REAL,
                score_300        INTEGER,
                score_100        INTEGER,
                score_50         INTEGER,
                miss_count       INTEGER,
                max_combo        INTEGER,
                FOREIGN KEY (user_id)  REFERENCES Users(user_id),
                FOREIGN KEY (track_id) REFERENCES MidiTracks(track_id)
            );",

            @"CREATE TABLE HitEvents (
                hit_id        INTEGER PRIMARY KEY AUTOINCREMENT,
                result_id     INTEGER NOT NULL,
                note_id       INTEGER NOT NULL,
                expected_time REAL,
                actual_time   REAL,
                delta_ms      INTEGER,
                judgement     TEXT CHECK(judgement IN ('perfect','good','bad','miss')),
                finger_used   INTEGER,
                FOREIGN KEY (result_id) REFERENCES GameResults(result_id),
                FOREIGN KEY (note_id)   REFERENCES Notes(note_id)
            );",

            @"CREATE TABLE UserSettings (
                user_id               INTEGER PRIMARY KEY,
                visual_offset_ms      INTEGER DEFAULT 0,
                audio_offset_ms       INTEGER DEFAULT 0,
                min_bpm_filter        INTEGER,
                max_bpm_filter        INTEGER,
                preferred_hand        TEXT DEFAULT 'auto',
                difficulty_filter_min REAL DEFAULT 1,
                difficulty_filter_max REAL DEFAULT 10,
                key_label_mode        TEXT DEFAULT 'off',
                note_label_mode       TEXT DEFAULT 'off',
                master_volume         REAL DEFAULT 1,
                FOREIGN KEY (user_id) REFERENCES Users(user_id)
            );",

            @"CREATE TABLE FingerMapping (
                mapping_id         INTEGER PRIMARY KEY AUTOINCREMENT,
                track_id           INTEGER NOT NULL,
                note_id            INTEGER NOT NULL,
                recommended_hand   TEXT,
                recommended_finger INTEGER,
                FOREIGN KEY (track_id) REFERENCES MidiTracks(track_id),
                FOREIGN KEY (note_id)  REFERENCES Notes(note_id)
            );",

            // ----- §2.2 Размерность клавиатуры и транспозиция -----
            @"CREATE TABLE DeviceProfiles (
                device_id    INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id      INTEGER NOT NULL,
                device_name  TEXT NOT NULL,
                lowest_note  INTEGER NOT NULL,
                highest_note INTEGER NOT NULL,
                total_keys   INTEGER NOT NULL,
                is_default   BOOLEAN DEFAULT 0,
                FOREIGN KEY (user_id) REFERENCES Users(user_id)
            );",

            @"CREATE TABLE TrackTransposition (
                transposition_id    INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id             INTEGER NOT NULL,
                track_id            INTEGER NOT NULL,
                device_id           INTEGER NOT NULL,
                transpose_semitones INTEGER DEFAULT 0,
                auto_transpose      BOOLEAN DEFAULT 1,
                created_at          DATETIME DEFAULT CURRENT_TIMESTAMP,
                last_used           DATETIME,
                UNIQUE(user_id, track_id, device_id),
                FOREIGN KEY (user_id)   REFERENCES Users(user_id),
                FOREIGN KEY (track_id)  REFERENCES MidiTracks(track_id),
                FOREIGN KEY (device_id) REFERENCES DeviceProfiles(device_id)
            );",

            @"CREATE TABLE RemappingRules (
                rule_id       INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id       INTEGER NOT NULL,
                device_id     INTEGER NOT NULL,
                original_note INTEGER NOT NULL,
                remapped_note INTEGER NOT NULL,
                is_active     BOOLEAN DEFAULT 1,
                FOREIGN KEY (user_id)   REFERENCES Users(user_id),
                FOREIGN KEY (device_id) REFERENCES DeviceProfiles(device_id)
            );",

            @"CREATE TABLE TranspositionHistory (
                history_id     INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id        INTEGER NOT NULL,
                track_id       INTEGER NOT NULL,
                device_id      INTEGER NOT NULL,
                transpose_used INTEGER NOT NULL,
                was_auto       BOOLEAN,
                timestamp      DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES Users(user_id)
            );",

            // ----- §2.3 Редактирование аппликатуры -----
            @"CREATE TABLE UserFingerAssignments (
                assignment_id   INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id         INTEGER NOT NULL,
                track_id        INTEGER NOT NULL,
                note_id         INTEGER NOT NULL,
                assigned_hand   TEXT CHECK(assigned_hand IN ('left','right')),
                assigned_finger INTEGER CHECK(assigned_finger BETWEEN 1 AND 5),
                is_edited       BOOLEAN DEFAULT 1,
                created_at      DATETIME DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(user_id, track_id, note_id),
                FOREIGN KEY (user_id)  REFERENCES Users(user_id),
                FOREIGN KEY (track_id) REFERENCES MidiTracks(track_id),
                FOREIGN KEY (note_id)  REFERENCES Notes(note_id)
            );",

            @"CREATE TABLE FingerTemplates (
                template_id    INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id        INTEGER NOT NULL,
                template_name  TEXT NOT NULL,
                hand           TEXT NOT NULL,
                finger_pattern TEXT,
                created_at     DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES Users(user_id)
            );",

            @"CREATE TABLE TemplateNoteMapping (
                mapping_id          INTEGER PRIMARY KEY AUTOINCREMENT,
                template_id         INTEGER NOT NULL,
                note_sequence_index INTEGER,
                midi_note_offset    INTEGER,
                finger              INTEGER,
                FOREIGN KEY (template_id) REFERENCES FingerTemplates(template_id)
            );",

            // ----- §2.7 Версионирование схемы -----
            @"CREATE TABLE SchemaVersion (
                version_id     INTEGER PRIMARY KEY,
                version_number INTEGER NOT NULL,
                applied_at     DATETIME DEFAULT CURRENT_TIMESTAMP,
                description    TEXT
            );",

            // ----- §2.5 Индексы -----
            @"CREATE INDEX idx_hits_result ON HitEvents(result_id);",
            @"CREATE INDEX idx_notes_track ON Notes(track_id);",
            @"CREATE INDEX idx_results_user_track ON GameResults(user_id, track_id);",
            @"CREATE INDEX idx_hits_judgement ON HitEvents(judgement);",
            @"CREATE INDEX idx_tracks_difficulty ON MidiTracks(difficulty, bpm);",

            // ----- §2.6 Триггеры (активный компонент БД) -----
            @"CREATE TRIGGER update_user_stats AFTER INSERT ON GameResults
              BEGIN
                  UPDATE Users SET
                      total_score = (SELECT AVG(accuracy_percent) FROM GameResults WHERE user_id = NEW.user_id),
                      games_played = games_played + 1
                  WHERE user_id = NEW.user_id;
              END;",

            @"CREATE TRIGGER update_track_range AFTER INSERT ON Notes
              BEGIN
                  UPDATE MidiTracks SET
                      min_required_keys = (SELECT MAX(midi_number) - MIN(midi_number) FROM Notes WHERE track_id = NEW.track_id),
                      recommended_octave_range = (
                          SELECT 'C' || ((MIN(midi_number) - 12) / 12) || '-C' || ((MAX(midi_number) - 12) / 12)
                          FROM Notes WHERE track_id = NEW.track_id
                      )
                  WHERE track_id = NEW.track_id;
              END;",
        };
    }
}
