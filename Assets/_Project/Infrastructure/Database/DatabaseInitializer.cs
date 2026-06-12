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
        /// В любом случае доводит существующую БД до текущей схемы (<see cref="Migrate"/>), т.к.
        /// рантайм-копия в persistentDataPath могла быть создана старой версией без новых колонок.
        /// </summary>
        public static void EnsureCreated(SQLiteConnection conn, bool seed = true)
        {
            if (!IsCreated(conn))
            {
                ApplySchema(conn);
                if (seed)
                    DatabaseSeeder.Seed(conn);
            }

            Migrate(conn);
        }

        /// <summary>
        /// Лёгкая миграция уже созданной БД до текущей схемы: добавляет колонки, появившиеся в новых
        /// версиях (ALTER TABLE ADD COLUMN идемпотентен — на свежей схеме колонки уже есть). Без этого
        /// старый notes.db ронял бы чтение настроек («no such column key_label_mode»).
        /// </summary>
        private static void Migrate(SQLiteConnection conn)
        {
            EnsureColumn(conn, "UserSettings", "key_label_mode", "TEXT DEFAULT 'off'");
            EnsureColumn(conn, "UserSettings", "note_label_mode", "TEXT DEFAULT 'off'");
        }

        private static void EnsureColumn(SQLiteConnection conn, string table, string column, string definition)
        {
            try
            {
                conn.Execute($"ALTER TABLE {table} ADD COLUMN {column} {definition};");
            }
            catch (SQLiteException)
            {
                // Колонка уже существует (свежая схема или повторный вызов) — это нормально.
            }
        }
    }
}
