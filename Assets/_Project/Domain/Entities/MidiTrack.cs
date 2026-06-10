namespace Racconotes.Domain.Entities
{
    /// <summary>
    /// Метаданные MIDI-трека (таблица MidiTracks, §2.1). POCO без атрибутов SQLite.
    /// </summary>
    public sealed class MidiTrack
    {
        public int TrackId { get; set; }
        public string Filename { get; set; }
        public string Title { get; set; }
        public string Composer { get; set; }
        public double Bpm { get; set; }
        public string Tonality { get; set; }   // C, G, Am и т.д.
        public double Difficulty { get; set; }  // 1.0 - 10.0
        public double NoteDensity { get; set; } // нот в секунду
    }
}
