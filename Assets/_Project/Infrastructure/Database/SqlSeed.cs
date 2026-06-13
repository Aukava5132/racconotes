namespace Racconotes.Infrastructure.Database
{
    /// <summary>
    /// Статические seed-данные (справочники) на чистом SQL: демо-пользователь, его настройки,
    /// несколько MIDI-треков и ноты. Явные первичные ключи — для детерминизма и удобной
    /// привязки демо-нажатий (<see cref="DatabaseSeeder"/>).
    ///
    /// Даты (created_at) НЕ задаём — остаются на DEFAULT CURRENT_TIMESTAMP (TEXT); они нигде
    /// не читаются в C# как DateTime на этом этапе. Игровые результаты с датами наполняет
    /// <see cref="DatabaseSeeder"/> программно (тиками), т.к. их played_at читается как DateTime.
    /// </summary>
    public static class SqlSeed
    {
        public static readonly string[] Statements =
        {
            // Демо-пользователь и его настройки
            @"INSERT INTO Users (user_id, username) VALUES (1, 'demo');",
            @"INSERT INTO UserSettings (user_id, min_bpm_filter, max_bpm_filter, preferred_hand)
              VALUES (1, 40, 200, 'auto');",

            // Треки
            @"INSERT INTO MidiTracks (track_id, filename, title, composer, bpm, tonality, difficulty, note_density)
              VALUES (1, 'ode_to_joy.mid', 'Ода до радості', 'Beethoven', 120, 'C', 2.5, 2.0);",
            @"INSERT INTO MidiTracks (track_id, filename, title, composer, bpm, tonality, difficulty, note_density)
              VALUES (2, 'moonlight.mid', 'Місячна соната', 'Beethoven', 60, 'C#m', 7.0, 1.5);",
            @"INSERT INTO MidiTracks (track_id, filename, title, composer, bpm, tonality, difficulty, note_density)
              VALUES (3, 'invention_1.mid', 'Інвенція №1', 'Bach', 100, 'C', 4.0, 3.0);",

            // Ноты трека 1 (правая рука) — note_id 1..6
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (1, 1, 0, 64, 0.0, 0.5, 'right', 3);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (2, 1, 1, 64, 0.5, 0.5, 'right', 3);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (3, 1, 2, 65, 1.0, 0.5, 'right', 4);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (4, 1, 3, 67, 1.5, 0.5, 'right', 5);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (5, 1, 4, 67, 2.0, 0.5, 'right', 5);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (6, 1, 5, 65, 2.5, 0.5, 'right', 4);",

            // Ноты трека 2 (левая рука) — note_id 7..10
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (7, 2, 0, 49, 0.0, 1.0, 'left', 5);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (8, 2, 1, 52, 1.0, 1.0, 'left', 3);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (9, 2, 2, 56, 2.0, 1.0, 'left', 1);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (10, 2, 3, 49, 3.0, 1.0, 'left', 5);",

            // Ноты трека 3 «Инвенция №1» (правая рука, мотив C-D-E-F-D-E-C-G) — note_id 11..18.
            // Раньше трек 3 был без нот, и кнопка «Играть» для него молча не срабатывала.
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (11, 3, 0, 60, 0.0, 0.5, 'right', 1);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (12, 3, 1, 62, 0.5, 0.5, 'right', 2);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (13, 3, 2, 64, 1.0, 0.5, 'right', 3);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (14, 3, 3, 65, 1.5, 0.5, 'right', 4);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (15, 3, 4, 62, 2.0, 0.5, 'right', 2);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (16, 3, 5, 64, 2.5, 0.5, 'right', 3);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (17, 3, 6, 60, 3.0, 0.5, 'right', 1);",
            @"INSERT INTO Notes (note_id, track_id, note_index, midi_number, start_time, duration, hand, finger)
              VALUES (18, 3, 7, 67, 3.5, 0.5, 'right', 5);",
        };
    }
}
