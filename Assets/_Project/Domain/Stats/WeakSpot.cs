namespace Racconotes.Domain.Stats
{
    /// <summary>
    /// «Слабое место» — нота, на которой чаще всего промах (результат Запроса 2, §2.4).
    /// POCO-результат аналитики; общий тип для in-memory <c>StatsAnalyzer</c> (Application)
    /// и SQL-реализации <c>IStatsQueries</c> (Infrastructure), поэтому живёт в Domain.
    /// </summary>
    public sealed class WeakSpot
    {
        public int MidiNumber { get; set; }
        public string Hand { get; set; }   // как в Note ('left'/'right'); может быть null
        public int Finger { get; set; }
        public int MissCount { get; set; }
    }
}
