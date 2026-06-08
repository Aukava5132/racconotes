# StreamingAssets

Сюда кладётся **seed-БД** `notes.db` (read-only в собранном билде).

Паттерн использования (Этап 2/3):
1. При первом запуске проверить наличие копии в `Application.persistentDataPath`.
2. Если нет — скопировать `notes.db` сюда из StreamingAssets (на Windows это реальный путь, работает прямой `File.Copy`).
3. Открыть копию в `persistentDataPath` с `SQLiteOpenFlags.ReadWrite | Create`, включить WAL.
4. Версионировать seed-БД (таблица `SchemaVersion`), чтобы при обновлении перекопировать/мигрировать.

Все записи (HitEvents, GameResults) пишутся в копию в `persistentDataPath`, а не в StreamingAssets.
