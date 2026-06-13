using Racconotes.Application;
using SQLite;
using UnityEngine;

namespace Racconotes.Composition
{
    /// <summary>
    /// Владелец жизненного цикла SQLite-соединения в рантайме. Автосоздаётся
    /// <see cref="RuntimeBootstrap"/> как <c>DontDestroyOnLoad</c>-объект и закрывает соединение
    /// при выходе из игры / Play Mode (хуки <see cref="OnApplicationQuit"/>/<see cref="OnDestroy"/>),
    /// чтобы не текли файловые хендлы и не оставался заблокированным <c>notes.db</c>.
    ///
    /// <see cref="Dispose"/> идемпотентен (флаг <see cref="_disposed"/>) — оба хука могут сработать,
    /// закрытие произойдёт ровно один раз. Статический <see cref="_instance"/> защищает от дубля
    /// при повторном входе в Play Mode с отключённым domain reload (старый хост закрывается первым).
    /// </summary>
    public sealed class GameContextHost : MonoBehaviour
    {
        private static GameContextHost _instance;

        private SQLiteConnection _conn;
        private bool _disposed;

        /// <summary>Создаёт хост, закрыв и убрав предыдущий (если статика пережила Play-сессию).</summary>
        public static GameContextHost Create(SQLiteConnection conn)
        {
            if (_instance != null)
            {
                _instance.Dispose();
                Destroy(_instance.gameObject);
                _instance = null;
            }

            var go = new GameObject("[Racconotes] GameContextHost");
            DontDestroyOnLoad(go);
            var host = go.AddComponent<GameContextHost>();
            host._conn = conn;
            _instance = host;
            return host;
        }

        private void OnApplicationQuit() => Dispose();

        private void OnDestroy()
        {
            Dispose();
            if (_instance == this)
                _instance = null;
        }

        private void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _conn?.Dispose();
            _conn = null;
            GameServices.Reset();
            Debug.Log("[Racconotes] З'єднання з БД закрито.");
        }
    }
}
