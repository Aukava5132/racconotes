namespace Racconotes.Domain.Stats
{
    /// <summary>
    /// Рекомендованный трек (результат Запроса 3, §2.4): давно не игранный и подходящий по уровню.
    /// Доменный тип результата аналитики для SQL-реализации <c>IStatsQueries</c>.
    /// </summary>
    public sealed class TrackRecommendation
    {
        public int TrackId { get; set; }
        public string Title { get; set; }
        public double Difficulty { get; set; }
        public double Bpm { get; set; }
    }
}
