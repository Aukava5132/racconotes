using UnityEngine;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Чистая логика подписей клавиш и летящих нот: текст по выбранному <see cref="LabelMode"/>
    /// и цвет текста по типу клавиши. Тестируется в EditMode без рендера.
    /// </summary>
    public static class NoteLabels
    {
        /// <summary>
        /// Текст подписи для ноты <paramref name="midi"/> в режиме <paramref name="mode"/>.
        /// <paramref name="baseMidi"/> нужен только для режима «Кнопки» (= <c>layout.LowMidi</c>).
        /// Пустая строка — режим Off или нота вне раскладки в режиме «Кнопки».
        /// </summary>
        public static string Format(int midi, int baseMidi, LabelMode mode)
        {
            switch (mode)
            {
                case LabelMode.KeyboardKey: return PianoKeyMap.KeyLabelFor(midi, baseMidi);
                case LabelMode.NoteName: return NoteNaming.NameNoOctave(midi);
                case LabelMode.Solfege: return NoteNaming.Solfege(midi);
                default: return "";
            }
        }

        /// <summary>На белой клавише/ноте — чёрный текст, на чёрной — белый.</summary>
        public static Color LabelColor(int midi) => PianoLayout.IsBlack(midi) ? Color.white : Color.black;
    }
}
