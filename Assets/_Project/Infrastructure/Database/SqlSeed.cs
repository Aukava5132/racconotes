namespace Racconotes.Infrastructure.Database
{
    /// <summary>
    /// Статические seed-данные на чистом SQL: демо-пользователь и его настройки. Встроенные треки
    /// и ноты вынесены в <see cref="BuiltInTrackCatalog"/> и вставляются через репозитории
    /// (<see cref="DatabaseSeeder"/>) — это позволяет той же самой моделью каталога идемпотентно
    /// синхронизировать встроенные треки в уже созданной БД, не трогая импортированные пользователем.
    ///
    /// Даты (created_at) НЕ задаём — остаются на DEFAULT CURRENT_TIMESTAMP (TEXT); они нигде
    /// не читаются в C# как DateTime на этом этапе. Игровые результаты с датами наполняет
    /// <see cref="DatabaseSeeder"/> программно (тиками), т.к. их played_at читается как DateTime.
    /// </summary>
    public static class SqlSeed
    {
        public static readonly string[] Statements =
        {
            // Демо-пользователь и его настройки
            @"INSERT INTO Users (user_id, username) VALUES (1, 'demo');",
            @"INSERT INTO UserSettings (user_id, min_bpm_filter, max_bpm_filter, preferred_hand)
              VALUES (1, 40, 200, 'auto');",
        };
    }
}
