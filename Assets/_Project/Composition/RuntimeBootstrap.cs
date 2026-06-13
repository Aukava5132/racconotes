using System.IO;
using System.Linq;
using Racconotes.Application;
using Racconotes.Infrastructure.Database;
using SQLite;
using UnityEngine;

namespace Racconotes.Composition
{
    /// <summary>
    /// Композиционный корень рантайма — точка входа Этапа 3. Стартует автоматически до загрузки
    /// первой сцены (без ручной правки .unity): открывает БД в <c>persistentDataPath/notes.db</c>,
    /// при первом запуске создаёт схему + seed (демо-треки/ноты/результаты), собирает граф
    /// зависимостей через <see cref="GameContextFactory"/> и публикует его в <see cref="GameServices"/>
    /// для слоя Presentation. Соединением далее владеет <see cref="GameContextHost"/>.
    ///
    /// Это рантайм-аналог editor-инструмента <c>Composition/Editor/MidiImportTool.cs</c>:
    /// тот же приём DI (открыть соединение → EnsureCreated → собрать сервисы на одном conn),
    /// но путь — <c>Application.persistentDataPath</c>, а не <c>Artifacts/</c>.
    /// </summary>
    public static class RuntimeBootstrap
    {
        private const string DbFileName = "notes.db";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            string dbPath = ResolveDbPath();

            // Открываем (файл создаётся при отсутствии, FK включаются внутри фабрики).
            SQLiteConnection conn = DatabaseConnectionFactory.Open(dbPath);
            // seed:true — чтобы свежая БД содержала демо-данные; идемпотентно при повторных запусках.
            DatabaseInitializer.EnsureCreated(conn, seed: true);

            GameContext context = GameContextFactory.Build(conn);

            // Хост создаём ДО публикации контекста: закрытие возможного устаревшего хоста
            // (сброс GameServices) не затрёт свежий контекст.
            GameContextHost.Create(conn);
            GameServices.Set(context);

            int trackCount = context.TrackRepository.GetAllTracks().Count();
            Debug.Log($"[Racconotes] БД готова: {dbPath}, схема v{DatabaseInitializer.CurrentSchemaVersion}, " +
                      $"треків={trackCount}");
        }

        public static string ResolveDbPath() =>
            Path.Combine(UnityEngine.Application.persistentDataPath, DbFileName);
    }
}
