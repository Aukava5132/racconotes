using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Раскладка компьютерной клавиатуры на фортепиано (две октавы, стандартная схема софт-пианино):
    /// нижний ряд Z S X D C V G B H N J M = C C# D D# E F F# G G# A A# B,
    /// верхний ряд Q 2 W 3 E R 5 T 6 Y 7 U = следующая октава. Чистая таблица «клавиша → полутон»;
    /// само чтение устройства — в <c>KeyboardInputSource</c>.
    /// </summary>
    public static class PianoKeyMap
    {
        private static readonly Dictionary<Key, int> Semitones = new Dictionary<Key, int>
        {
            // Нижняя октава
            { Key.Z, 0 }, { Key.S, 1 }, { Key.X, 2 }, { Key.D, 3 }, { Key.C, 4 },
            { Key.V, 5 }, { Key.G, 6 }, { Key.B, 7 }, { Key.H, 8 }, { Key.N, 9 },
            { Key.J, 10 }, { Key.M, 11 },
            // Верхняя октава
            { Key.Q, 12 }, { Key.Digit2, 13 }, { Key.W, 14 }, { Key.Digit3, 15 }, { Key.E, 16 },
            { Key.R, 17 }, { Key.Digit5, 18 }, { Key.T, 19 }, { Key.Digit6, 20 }, { Key.Y, 21 },
            { Key.Digit7, 22 }, { Key.U, 23 },
        };

        // Человекочитаемые подписи кнопок (Key.Digit2.ToString() == "Digit2", нужно "2").
        // Ключи синхронны с Semitones — обратная таблица «полутон → подпись» строится по ним.
        private static readonly Dictionary<Key, string> Captions = new Dictionary<Key, string>
        {
            { Key.Z, "Z" }, { Key.S, "S" }, { Key.X, "X" }, { Key.D, "D" }, { Key.C, "C" },
            { Key.V, "V" }, { Key.G, "G" }, { Key.B, "B" }, { Key.H, "H" }, { Key.N, "N" },
            { Key.J, "J" }, { Key.M, "M" },
            { Key.Q, "Q" }, { Key.Digit2, "2" }, { Key.W, "W" }, { Key.Digit3, "3" }, { Key.E, "E" },
            { Key.R, "R" }, { Key.Digit5, "5" }, { Key.T, "T" }, { Key.Digit6, "6" }, { Key.Y, "Y" },
            { Key.Digit7, "7" }, { Key.U, "U" },
        };

        // Обратная таблица «полутон → подпись кнопки» (полутон в Semitones уникален).
        private static readonly Dictionary<int, string> SemitoneCaptions = BuildSemitoneCaptions();

        private static Dictionary<int, string> BuildSemitoneCaptions()
        {
            var map = new Dictionary<int, string>();
            foreach (var pair in Semitones)
                map[pair.Value] = Captions[pair.Key];
            return map;
        }

        /// <summary>Все клавиши, задействованные под ноты (для опроса в Update).</summary>
        public static IEnumerable<Key> MappedKeys => Semitones.Keys;

        /// <summary>
        /// MIDI-номер для клавиши при базовой C отображаемого диапазона (<paramref name="baseMidi"/>),
        /// либо null, если клавиша не назначена на ноту.
        /// </summary>
        public static int? MidiFor(Key key, int baseMidi)
            => Semitones.TryGetValue(key, out int semis) ? baseMidi + semis : (int?)null;

        /// <summary>
        /// Подпись физической кнопки клавиатуры для ноты <paramref name="midi"/> при базовой C
        /// диапазона (<paramref name="baseMidi"/> = <c>layout.LowMidi</c>): «Z», «S», … «2» … «U».
        /// Пустая строка, если нота вне двух октав раскладки (физической кнопки нет).
        /// </summary>
        public static string KeyLabelFor(int midi, int baseMidi)
            => SemitoneCaptions.TryGetValue(midi - baseMidi, out string caption) ? caption : "";
    }
}
