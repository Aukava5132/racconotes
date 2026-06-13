using System;
using System.IO;
using Racconotes.Application.Midi;
using Racconotes.Infrastructure.Database;
using Racconotes.Infrastructure.Repositories;
using SQLite;
using UnityEditor;
using UnityEngine;

namespace Racconotes.Composition.Editor
{
    /// <summary>
    /// Композиционный корень для импорта MIDI из редактора: связывает чистый сервис из Application
    /// (<see cref="MidiImportService"/>) с конкретными SQLite-репозиториями из Infrastructure.
    /// Живёт в Composition, потому что только этот слой видит и Application, и Infrastructure
    /// одновременно (Infrastructure ↮ Application — иначе нарушение слоёв = ошибка компиляции).
    /// Импорт идёт в ту же seed-БД <c>Artifacts/notes.db</c>, что создаёт DatabaseExportTool.
    /// </summary>
    public static class MidiImportTool
    {
        private const string MenuPath = "Racconotes/Імпорт MIDI → БД…";

        [MenuItem(MenuPath)]
        public static void ImportMidi()
        {
            string sourcePath = EditorUtility.OpenFilePanel("Виберіть MIDI-файл", "", "mid,midi");
            if (string.IsNullOrEmpty(sourcePath))
                return; // пользователь отменил выбор

            try
            {
                byte[] bytes = File.ReadAllBytes(sourcePath);
                string dbPath = ResolveDbPath();

                using SQLiteConnection conn = DatabaseConnectionFactory.Open(dbPath);
                DatabaseInitializer.EnsureCreated(conn, seed: false);

                var service = new MidiImportService(
                    new SqliteTrackRepository(conn),
                    new SqliteNoteRepository(conn));

                int trackId = service.ImportFromBytes(bytes, Path.GetFileName(sourcePath));
                int noteCount = conn.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM Notes WHERE track_id = ?;", trackId);

                Debug.Log($"[Racconotes] Імпортовано MIDI «{Path.GetFileName(sourcePath)}» → " +
                          $"track_id={trackId}, нот: {noteCount}. БД: {dbPath}");
                EditorUtility.RevealInFinder(dbPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Racconotes] Помилка імпорту MIDI: {ex.Message}");
            }
        }

        private static string ResolveDbPath()
        {
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName; // корень проекта
            string dir = Path.Combine(root, "Artifacts");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "notes.db");
        }
    }
}
