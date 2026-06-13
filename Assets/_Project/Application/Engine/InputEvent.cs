namespace Racconotes.Application.Engine
{
    /// <summary>
    /// Одно нажатие пользователя: момент времени (мс), высота (MIDI-номер) и палец.
    /// Детерминированный вход игрового цикла — не зависит от Unity.
    /// </summary>
    public readonly struct InputEvent
    {
        public double TimeMs { get; }
        public int MidiNumber { get; }
        public int FingerUsed { get; }

        public InputEvent(double timeMs, int midiNumber, int fingerUsed = 0)
        {
            TimeMs = timeMs;
            MidiNumber = midiNumber;
            FingerUsed = fingerUsed;
        }
    }
}
