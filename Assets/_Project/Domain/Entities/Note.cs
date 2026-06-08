namespace Racconotes.Domain.Entities
{
    /// <summary>
    /// Нота из таблицы Notes (см. §2.1 задания). Игра читает ноты ТОЛЬКО из БД.
    /// POCO без атрибутов SQLite — слой Domain не знает о способе хранения (DIP).
    /// </summary>
    public sealed class Note
    {
        public int NoteId { get; set; }
        public int TrackId { get; set; }
        public int NoteIndex { get; set; }
        public int MidiNumber { get; set; }   // 60 = C4
        public double StartTime { get; set; }  // секунды от начала трека
        public double Duration { get; set; }   // секунды (для удержания)
        public string Hand { get; set; }       // "left" | "right"
        public int Finger { get; set; }        // 1..5
    }
}
