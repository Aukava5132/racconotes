namespace Racconotes.Domain.Entities
{
    /// <summary>
    /// Пользовательское переопределение аппликатуры ноты (таблица UserFingerAssignments, §2.3).
    /// Переопределяет рекомендованные руку/палец из <see cref="Note"/>; исходные Notes не меняются,
    /// а при загрузке нот переопределение применяется через LEFT JOIN + COALESCE
    /// (БД — активный компонент). POCO без атрибутов SQLite (DIP).
    /// </summary>
    public sealed class UserFingerAssignment
    {
        public int AssignmentId { get; set; }
        public int UserId { get; set; }
        public int TrackId { get; set; }
        public int NoteId { get; set; }
        public string AssignedHand { get; set; }   // "left" | "right"
        public int AssignedFinger { get; set; }     // 1..5
        public bool IsEdited { get; set; }
    }
}
