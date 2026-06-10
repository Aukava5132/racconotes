using System;
using System.Collections.Generic;

namespace Racconotes.Application.Midi
{
    /// <summary>
    /// Вычисление метаданных трека из разобранных нот: плотность нот (нот/сек) и оценка сложности
    /// 1..10. Формула прозрачная и монотонная по плотности — её легко обосновать на защите. Поля
    /// можно скорректировать вручную через UpdateTrack; в БД difficulty/note_density nullable.
    /// </summary>
    public static class MidiDifficulty
    {
        private const double MinSpanSeconds = 0.001;

        /// <summary>Нот в секунду на протяжении звучания трека (0, если нот нет).</summary>
        public static double NoteDensity(IReadOnlyList<RawMidiNote> notes)
        {
            if (notes == null || notes.Count == 0) return 0.0;
            return notes.Count / TotalSeconds(notes);
        }

        /// <summary>Оценка сложности 1.0..10.0 из плотности нот и диапазона высот.</summary>
        public static double EstimateDifficulty(IReadOnlyList<RawMidiNote> notes)
        {
            if (notes == null || notes.Count == 0) return 1.0;

            double density = NoteDensity(notes);

            int min = int.MaxValue, max = int.MinValue;
            foreach (RawMidiNote note in notes)
            {
                if (note.MidiNumber < min) min = note.MidiNumber;
                if (note.MidiNumber > max) max = note.MidiNumber;
            }
            int pitchRange = max - min;

            double raw = 1.0 + density * 1.5 + (pitchRange / 12.0) * 0.5;
            return Clamp(raw, 1.0, 10.0);
        }

        private static double TotalSeconds(IReadOnlyList<RawMidiNote> notes)
        {
            double end = 0.0;
            foreach (RawMidiNote note in notes)
            {
                double noteEnd = note.StartSeconds + note.DurationSeconds;
                if (noteEnd > end) end = noteEnd;
            }
            return Math.Max(end, MinSpanSeconds);
        }

        private static double Clamp(double value, double min, double max) =>
            value < min ? min : (value > max ? max : value);
    }
}
