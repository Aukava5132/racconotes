namespace Racconotes.Domain.Entities
{
    /// <summary>
    /// Настройки пользователя, влияющие на геймплей (таблица UserSettings, §2.1).
    /// POCO без атрибутов SQLite.
    /// </summary>
    public sealed class UserSettings
    {
        public int UserId { get; set; }
        public int VisualOffsetMs { get; set; }       // смещение нот
        public int AudioOffsetMs { get; set; }
        public int MinBpmFilter { get; set; }
        public int MaxBpmFilter { get; set; }
        public string PreferredHand { get; set; }      // left/right/auto
        public double DifficultyFilterMin { get; set; }
        public double DifficultyFilterMax { get; set; }
        public string KeyLabelMode { get; set; }       // режим подписи клавиш: off/key/note/solfege
        public string NoteLabelMode { get; set; }      // режим подписи летящих нот: off/key/note/solfege
    }
}
