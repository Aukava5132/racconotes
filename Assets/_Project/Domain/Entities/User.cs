using System;

namespace Racconotes.Domain.Entities
{
    /// <summary>
    /// Профиль пользователя (таблица Users, §2.1). POCO без атрибутов SQLite.
    /// </summary>
    public sealed class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public DateTime CreatedAt { get; set; }
        public double TotalScore { get; set; }   // денормализовано для скорости
        public int GamesPlayed { get; set; }
    }
}
