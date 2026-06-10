using Racconotes.Domain.Enums;

namespace Racconotes.Domain.Entities
{
    /// <summary>
    /// Одно нажатие, сопоставленное с нотой (таблица HitEvents, §2.1). POCO без атрибутов SQLite.
    /// Для пропущенной ноты (Miss без ввода) <see cref="ActualTime"/> = <see cref="double.NaN"/>.
    /// </summary>
    public sealed class HitEvent
    {
        public int HitId { get; set; }
        public int ResultId { get; set; }
        public int NoteId { get; set; }
        public double ExpectedTime { get; set; }  // ожидаемое время нажатия (мс)
        public double ActualTime { get; set; }     // фактическое время (мс); NaN = ноты не было нажато
        public int DeltaMs { get; set; }           // ActualTime − ExpectedTime; отрицательное = рано
        public Judgement Judgement { get; set; }
        public int FingerUsed { get; set; }        // 1..5
    }
}
