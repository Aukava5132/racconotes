namespace Racconotes.Presentation
{
    /// <summary>
    /// Имя ноты по MIDI-номеру в научной нотации (C4 = 60), диезная запись.
    /// Чистый C# без рендера — тестируется в EditMode. Используется на экране статистики
    /// для подписи «слабых нот» (Запрос 2 §2.4), где из БД приходит только midi_number.
    /// </summary>
    public static class NoteNaming
    {
        private static readonly string[] Names =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private static readonly string[] Solfege_ =
            { "до", "до#", "ре", "ре#", "мі", "фа", "фа#", "соль", "соль#", "ля", "ля#", "сі" };

        /// <summary>Например, 60 → «C4», 61 → «C#4», 69 → «A4», 72 → «C5».</summary>
        public static string Name(int midi)
        {
            int octave = midi / 12 - 1; // MIDI 60 = C4, поэтому −1
            return NameNoOctave(midi) + octave;
        }

        /// <summary>Английское имя без номера октавы: 60 → «C», 61 → «C#», 71 → «B».</summary>
        public static string NameNoOctave(int midi) => Names[PitchClass(midi)];

        /// <summary>Сольфеджио без номера октавы: 60 → «до», 61 → «до#», 62 → «ре», 71 → «си».</summary>
        public static string Solfege(int midi) => Solfege_[PitchClass(midi)];

        private static int PitchClass(int midi) => ((midi % 12) + 12) % 12;
    }
}
