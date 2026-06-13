using Racconotes.Application;
using Racconotes.Application.Engine;
using Racconotes.Application.Midi;
using Racconotes.Application.Stats;
using Racconotes.Infrastructure.Repositories;
using SQLite;

namespace Racconotes.Composition
{
    /// <summary>
    /// Собирает граф зависимостей рантайма из одного открытого SQLite-соединения: связывает
    /// конкретные репозитории Infrastructure с сервисами Application и упаковывает их в
    /// <see cref="GameContext"/>. Это и есть композиционный корень (тут единственное место,
    /// где Application встречается с Infrastructure — DIP).
    ///
    /// Метод чистый: не трогает MonoBehaviour и Application.persistentDataPath, поэтому
    /// тестируется headless на <see cref="Infrastructure.Database.DatabaseConnectionFactory.OpenInMemory"/>.
    /// Открытие соединения и его жизненный цикл — задача <see cref="RuntimeBootstrap"/>/<see cref="GameContextHost"/>.
    /// </summary>
    public static class GameContextFactory
    {
        public static GameContext Build(SQLiteConnection conn)
        {
            var trackRepo = new SqliteTrackRepository(conn);
            var noteRepo = new SqliteNoteRepository(conn);
            var resultRepo = new SqliteResultRepository(conn);
            var statsQueries = new SqliteStatsQueries(conn);
            var settingsRepo = new SqliteUserSettingsRepository(conn);
            var fingerRepo = new SqliteUserFingerAssignmentRepository(conn);

            var session = new SessionController(noteRepo, resultRepo, new GameEngine());
            var midiImport = new MidiImportService(trackRepo, noteRepo);
            var statsAnalyzer = new StatsAnalyzer();

            return new GameContext(
                trackRepo, noteRepo, resultRepo, statsQueries, settingsRepo, fingerRepo,
                session, midiImport, statsAnalyzer);
        }
    }
}
