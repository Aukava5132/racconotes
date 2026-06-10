using System.IO;
using Racconotes.Infrastructure.Database;
using SQLite;
using UnityEditor;
using UnityEngine;

namespace Racconotes.Infrastructure.Editor
{
    /// <summary>
    /// Editor-утилита: строит свежую seed-БД (схема §2.1-2.7 + демо-данные) в файл вне Assets
    /// — для отчёта/ER-диаграммы и ручного просмотра в DB Browser. Бинарный .db в git не кладём;
    /// файл создаётся в каталоге Artifacts/ в корне проекта по требованию.
    /// </summary>
    public static class DatabaseExportTool
    {
        private const string MenuPath = "Racconotes/Экспорт seed-БД (notes.db)";

        [MenuItem(MenuPath)]
        public static void ExportSeedDatabase()
        {
            string path = Export();
            Debug.Log($"[Racconotes] seed-БД создана: {path}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// Точка для headless-вызова:
        /// <c>-executeMethod Racconotes.Infrastructure.Editor.DatabaseExportTool.ExportFromCli</c>.
        /// </summary>
        public static void ExportFromCli()
        {
            string path = Export();
            Debug.Log($"[Racconotes] seed-БД создана (CLI): {path}");
        }

        private static string Export()
        {
            string root = Directory.GetParent(Application.dataPath).FullName; // корень проекта
            string dir = Path.Combine(root, "Artifacts");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "notes.db");

            if (File.Exists(path))
                File.Delete(path);

            using SQLiteConnection conn = DatabaseConnectionFactory.Open(path);
            DatabaseInitializer.EnsureCreated(conn, seed: true);
            return path;
        }
    }
}
