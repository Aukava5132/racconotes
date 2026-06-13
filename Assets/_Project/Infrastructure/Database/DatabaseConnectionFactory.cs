using SQLite;

namespace Racconotes.Infrastructure.Database
{
    /// <summary>
    /// Единственная точка открытия соединений к БД. Включает PRAGMA foreign_keys=ON
    /// (в SQLite по умолчанию OFF и не персистится в файл — нужно на КАЖДОМ соединении,
    /// иначе ON DELETE CASCADE и проверки FK из схемы §2.1 не работают).
    ///
    /// Даты хранятся тиками (storeDateTimeAsTicks=true — поведение по умолчанию gilzoide
    /// sqlite-net): из C# всегда пишем DateTime явным параметром, не полагаясь на
    /// DEFAULT CURRENT_TIMESTAMP (он пишет TEXT, несовместимо с чтением тиков).
    /// </summary>
    public static class DatabaseConnectionFactory
    {
        public static SQLiteConnection Open(string path)
        {
            var conn = new SQLiteConnection(path);
            conn.Execute("PRAGMA foreign_keys=ON;");
            return conn;
        }

        /// <summary>In-memory БД с включёнными FK — для тестов.</summary>
        public static SQLiteConnection OpenInMemory() => Open(":memory:");
    }
}
