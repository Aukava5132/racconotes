using SQLite;

namespace Racconotes.Infrastructure.Database
{
    /// <summary>
    /// Создаёт и инициализирует БД: применяет DDL (§2.1-2.7) и при необходимости seed.
    /// Идемпотентно — повторный вызов на уже созданной БД ничего не делает (проверка
    /// по наличию таблицы SchemaVersion). Рантайм-копия БД живёт в Application.persistentDataPath;
    /// само открытие соединения с включёнными FK — через <see cref="DatabaseConnectionFactory"/>.
    /// </summary>
    public static class DatabaseInitializer
    {
        public const int CurrentSchemaVersion = SqlSchema.Version;

        /// <summary>Выполняет весь DDL и фиксирует версию схемы (§2.7). БД должна быть пустой.</summary>
        public static void ApplySchema(SQLiteConnection conn)
        {
            foreach (string statement in SqlSchema.Statements)
                conn.Execute(statement);

            conn.Execute(
                "INSERT INTO SchemaVersion (version_number, description) VALUES (?, ?);",
                SqlSchema.Version, "Базовая игра + аппликатура");
        }

        /// <summary>Создана ли уже схема (есть таблица SchemaVersion).</summary>
        public static bool IsCreated(SQLiteConnection conn) =>
            conn.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersion';") > 0;

        /// <summary>
        /// Гарантирует созданную БД: если схемы нет — применяет её и (опц.) наполняет seed-данными.
        /// </summary>
        public static void EnsureCreated(SQLiteConnection conn, bool seed = true)
        {
            if (IsCreated(conn))
                return;

            ApplySchema(conn);
            if (seed)
                DatabaseSeeder.Seed(conn);
        }
    }
}
