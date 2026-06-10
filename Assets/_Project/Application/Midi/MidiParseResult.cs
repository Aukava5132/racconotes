using System.Collections.Generic;

namespace Racconotes.Application.Midi
{
    /// <summary>
    /// Результат разбора MIDI-файла: список нот на единой временной шкале (в секундах),
    /// темп и тональность, извлечённые из мета-событий. Дальше <see cref="MidiImportService"/>
    /// строит из этого <c>MidiTrack</c> + <c>Note</c> и пишет в БД.
    /// </summary>
    public sealed class MidiParseResult
    {
        public MidiParseResult(IReadOnlyList<RawMidiNote> notes, double bpm, string tonality, int ticksPerQuarterNote)
        {
            Notes = notes;
            Bpm = bpm;
            Tonality = tonality;
            TicksPerQuarterNote = ticksPerQuarterNote;
        }

        /// <summary>Ноты, отсортированные по времени начала.</summary>
        public IReadOnlyList<RawMidiNote> Notes { get; }

        /// <summary>Темп первого Set-Tempo события (ударов в минуту); 120 по умолчанию.</summary>
        public double Bpm { get; }

        /// <summary>Тональность из Key-Signature события ("C", "Am", "G"…); <c>null</c>, если события нет.</summary>
        public string Tonality { get; }

        /// <summary>Division заголовка MThd — тиков на четвертную ноту (PPQ).</summary>
        public int TicksPerQuarterNote { get; }
    }
}
