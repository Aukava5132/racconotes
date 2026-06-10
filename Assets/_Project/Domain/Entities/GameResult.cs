using System;

namespace Racconotes.Domain.Entities
{
    /// <summary>
    /// Результат одной игровой сессии (таблица GameResults, §2.1). POCO без атрибутов SQLite.
    /// </summary>
    public sealed class GameResult
    {
        public int ResultId { get; set; }
        public int UserId { get; set; }
        public int TrackId { get; set; }
        public DateTime PlayedAt { get; set; }
        public double AccuracyPercent { get; set; } // 0-100
        public int Score300 { get; set; }           // кол-во Perfect
        public int Score100 { get; set; }           // кол-во Good
        public int Score50 { get; set; }            // кол-во Bad
        public int MissCount { get; set; }
        public int MaxCombo { get; set; }
    }
}
