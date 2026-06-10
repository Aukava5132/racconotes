using System.Collections.Generic;
using System.Linq;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Геометрия фортепианной клавиатуры (Synthesia-стиль) по диапазону высот трека.
    /// Диапазон расширяется до границ октав (от C октавы самой низкой ноты до B октавы самой
    /// высокой, минимум одна октава). Белые клавиши равной ширины встык; чёрные уже и смещены
    /// на правый край белой клавиши снизу. Чистый C# — тестируется в EditMode без рендера.
    ///
    /// Координаты в мировых единицах; клавиатура центрирована по X относительно нуля.
    /// </summary>
    public sealed class PianoLayout
    {
        private static readonly HashSet<int> BlackPitchClasses = new HashSet<int> { 1, 3, 6, 8, 10 };

        public int LowMidi { get; }
        public int HighMidi { get; }
        public int WhiteCount { get; }
        public float WhiteWidth { get; }
        public float BlackWidth { get; }
        public float LeftEdge { get; }
        public float TotalWidth { get; }

        public PianoLayout(IEnumerable<int> pitches, float whiteWidth = 1f, float blackWidthRatio = 0.6f)
        {
            List<int> list = pitches?.ToList() ?? new List<int>();
            int min = list.Count > 0 ? list.Min() : 60; // по умолчанию C4
            int max = list.Count > 0 ? list.Max() : 71; // B4

            int low = min - Mod12(min);            // C на/ниже min
            int high = max + (11 - Mod12(max));     // B на/выше max
            if (high - low < 11) high = low + 11;    // минимум одна октава

            LowMidi = low;
            HighMidi = high;
            WhiteWidth = whiteWidth;
            BlackWidth = whiteWidth * blackWidthRatio;
            WhiteCount = CountWhites(low, high);
            TotalWidth = WhiteCount * whiteWidth;
            LeftEdge = -TotalWidth / 2f;
        }

        /// <summary>Чёрная ли клавиша (диез/бемоль) по MIDI-номеру.</summary>
        public static bool IsBlack(int midi) => BlackPitchClasses.Contains(Mod12(midi));

        /// <summary>X-центр клавиши данной высоты в мировых единицах.</summary>
        public float XForMidi(int midi)
        {
            if (IsBlack(midi))
                return XForMidi(midi - 1) + WhiteWidth / 2f; // на стыке белых, midi-1 всегда белая

            int whiteIndex = CountWhites(LowMidi, midi - 1); // белых строго ниже данной
            return LeftEdge + (whiteIndex + 0.5f) * WhiteWidth;
        }

        /// <summary>Ширина клавиши данной высоты.</summary>
        public float WidthForMidi(int midi) => IsBlack(midi) ? BlackWidth : WhiteWidth;

        public bool InRange(int midi) => midi >= LowMidi && midi <= HighMidi;

        private static int Mod12(int n) => ((n % 12) + 12) % 12;

        /// <summary>Число белых клавиш в [from, to] включительно (to &lt; from → 0).</summary>
        private static int CountWhites(int from, int to)
        {
            int count = 0;
            for (int m = from; m <= to; m++)
                if (!IsBlack(m)) count++;
            return count;
        }
    }
}
