namespace Racconotes.Presentation
{
    /// <summary>
    /// Режим текстовой подписи клавиш/нот при игре. Чисто презентационное понятие
    /// (в т.ч. «кнопка клавиатуры»), поэтому enum живёт в Presentation; в БД режим
    /// хранится строкой (см. <see cref="LabelModeCodec"/>), enum в нижние слои не утекает.
    /// </summary>
    public enum LabelMode
    {
        Off,         // без подписей
        KeyboardKey, // физические кнопки клавиатуры (Z, S, X…)
        NoteName,    // английские названия нот (C, C#, D…)
        Solfege,     // сольфеджио (до, до#, ре…)
    }

    /// <summary>Маппинг <see cref="LabelMode"/> ↔ строка БД и человекочитаемая подпись для UI.</summary>
    public static class LabelModeCodec
    {
        public static string ToDbString(LabelMode mode)
        {
            switch (mode)
            {
                case LabelMode.KeyboardKey: return "key";
                case LabelMode.NoteName: return "note";
                case LabelMode.Solfege: return "solfege";
                default: return "off";
            }
        }

        /// <summary>Неизвестное значение или <c>null</c> → <see cref="LabelMode.Off"/>.</summary>
        public static LabelMode FromDbString(string value)
        {
            switch (value)
            {
                case "key": return LabelMode.KeyboardKey;
                case "note": return LabelMode.NoteName;
                case "solfege": return LabelMode.Solfege;
                default: return LabelMode.Off;
            }
        }

        public static string RuName(LabelMode mode)
        {
            switch (mode)
            {
                case LabelMode.KeyboardKey: return "Кнопки";
                case LabelMode.NoteName: return "Ноты";
                case LabelMode.Solfege: return "Сольфеджио";
                default: return "Выкл";
            }
        }
    }
}
