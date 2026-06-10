using System;
using System.Collections.Generic;
using System.IO;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;
using Racconotes.Domain.Repositories;

namespace Racconotes.Application.Midi
{
    /// <summary>
    /// Сценарий «Управление библиотекой MIDI» (§1.4): парсит MIDI-файл и сохраняет трек + ноты в БД.
    /// Реализует принцип активной БД (§2) — после импорта игра читает ноты ТОЛЬКО из таблицы Notes.
    /// Зависит лишь от интерфейсов репозиториев (DIP); конкретные SQLite-реализации инжектит
    /// композиционный корень. Аппликатура и сложность вычисляются автоматически при импорте.
    /// </summary>
    public sealed class MidiImportService
    {
        private readonly ITrackRepository _trackRepo;
        private readonly INoteRepository _noteRepo;

        public MidiImportService(ITrackRepository trackRepo, INoteRepository noteRepo)
        {
            _trackRepo = trackRepo ?? throw new ArgumentNullException(nameof(trackRepo));
            _noteRepo = noteRepo ?? throw new ArgumentNullException(nameof(noteRepo));
        }

        /// <summary>Парсит байты MIDI-файла и импортирует трек. Возвращает track_id нового трека.</summary>
        public int ImportFromBytes(byte[] fileBytes, string filename, string title = null, string composer = null)
        {
            MidiParseResult parsed = SmfParser.Parse(fileBytes);
            return Import(parsed, filename, title, composer);
        }

        /// <summary>Сохраняет уже разобранный MIDI в БД. Возвращает track_id нового трека.</summary>
        public int Import(MidiParseResult parsed, string filename, string title = null, string composer = null)
        {
            if (parsed == null) throw new ArgumentNullException(nameof(parsed));

            var track = new MidiTrack
            {
                Filename = filename,
                Title = string.IsNullOrEmpty(title) ? TitleFromFilename(filename) : title,
                Composer = composer,
                Bpm = parsed.Bpm,
                Tonality = parsed.Tonality,
                Difficulty = MidiDifficulty.EstimateDifficulty(parsed.Notes),
                NoteDensity = MidiDifficulty.NoteDensity(parsed.Notes)
            };

            int trackId = _trackRepo.AddTrack(track);

            var notes = new List<Note>(parsed.Notes.Count);
            for (int i = 0; i < parsed.Notes.Count; i++)
            {
                RawMidiNote raw = parsed.Notes[i];
                (Hand hand, int finger) = AutoFingering.Assign(raw.MidiNumber);
                notes.Add(new Note
                {
                    TrackId = trackId,
                    NoteIndex = i,
                    MidiNumber = raw.MidiNumber,
                    StartTime = raw.StartSeconds,
                    Duration = raw.DurationSeconds,
                    Hand = hand.ToString().ToLowerInvariant(), // "left" | "right" — формат БД (CHECK)
                    Finger = finger
                });
            }

            // Транзакционная вставка; триггер update_track_range заполнит диапазон клавиш трека.
            _noteRepo.BulkInsertNotes(notes);
            return trackId;
        }

        private static string TitleFromFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "Без названия";
            string name = Path.GetFileNameWithoutExtension(filename);
            return string.IsNullOrEmpty(name) ? "Без названия" : name;
        }
    }
}
