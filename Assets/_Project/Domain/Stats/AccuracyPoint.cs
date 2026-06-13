using System;

namespace Racconotes.Domain.Stats
{
    /// <summary>
    /// Точка прогресса точности во времени (результат Запроса 1 с LAG/OVER, §2.4).
    /// Общий доменный тип результата аналитики (in-memory и SQL).
    /// </summary>
    public sealed class AccuracyPoint
    {
        public DateTime PlayedAt { get; set; }
        public double Accuracy { get; set; }
        public double? PrevAccuracy { get; set; }
        public double? Improvement { get; set; } // Accuracy − PrevAccuracy
    }
}
