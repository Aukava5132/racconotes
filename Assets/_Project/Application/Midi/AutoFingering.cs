using Racconotes.Domain.Enums;

namespace Racconotes.Application.Midi
{
    /// <summary>
    /// Простая автоаппликатура по умолчанию для импортируемых нот (§2.3 — «простой алгоритм»).
    /// Это лишь рекомендация: пользователь переопределяет её позже через UserFingerAssignments.
    /// Рука выбирается по высоте относительно среднего До (C4 = 60), палец 1..5 — по положению
    /// ноты внутри октавы (для правой руки большой палец на нижних нотах, мизинец на верхних;
    /// для левой — зеркально). Алгоритм детерминированный.
    /// </summary>
    public static class AutoFingering
    {
        /// <summary>Граница рук: ноты ниже C4 (60) — левой рукой, остальные — правой.</summary>
        public const int HandSplitMidi = 60;

        public static (Hand Hand, int Finger) Assign(int midiNumber)
        {
            Hand hand = midiNumber < HandSplitMidi ? Hand.Left : Hand.Right;

            int positionInOctave = ((midiNumber % 12) + 12) % 12; // 0..11, безопасно для отрицательных
            int step = positionInOctave * 5 / 12;                 // 0..4
            int finger = hand == Hand.Right ? step + 1 : 5 - step; // всегда в [1..5]

            return (hand, finger);
        }

        public static (Hand Hand, int Finger) Assign(RawMidiNote note) => Assign(note.MidiNumber);
    }
}
