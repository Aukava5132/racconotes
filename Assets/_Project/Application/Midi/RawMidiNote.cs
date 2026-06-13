namespace Racconotes.Application.Midi
{
    /// <summary>
    /// Сырая нота, извлечённая из MIDI-файла парсером (<see cref="SmfParser"/>) — ещё без аппликатуры
    /// и без привязки к треку в БД. Время уже переведено из тиков в секунды по карте темпа.
    /// </summary>
    public readonly struct RawMidiNote
    {
        public RawMidiNote(int midiNumber, double startSeconds, double durationSeconds)
        {
            MidiNumber = midiNumber;
            StartSeconds = startSeconds;
            DurationSeconds = durationSeconds;
        }

        /// <summary>MIDI-номер высоты (60 = C4).</summary>
        public int MidiNumber { get; }

        /// <summary>Время начала ноты в секундах от старта трека.</summary>
        public double StartSeconds { get; }

        /// <summary>Длительность ноты в секундах (Note On → Note Off).</summary>
        public double DurationSeconds { get; }
    }
}
