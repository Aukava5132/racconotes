namespace Racconotes.Domain.Enums
{
    /// <summary>
    /// Рука для исполнения ноты. В БД хранится строкой ('left'/'right'),
    /// здесь — типобезопасное представление для бизнес-логики.
    /// </summary>
    public enum Hand
    {
        Left,
        Right
    }
}
