namespace Racconotes.Domain.Stats
{
    /// <summary>
    /// Средняя задержка по руке/пальцу (результат Запроса 4, §2.4).
    /// Общий доменный тип результата аналитики (in-memory и SQL).
    /// </summary>
    public sealed class LatencyByFinger
    {
        public string Hand { get; set; }
        public int Finger { get; set; }
        public double AvgDeltaMs { get; set; } // >0 = опаздывает, <0 = торопится
        public int HitCount { get; set; }
    }
}
