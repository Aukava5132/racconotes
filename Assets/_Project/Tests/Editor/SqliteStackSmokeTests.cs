using System.Collections.Generic;
using NUnit.Framework;
using SQLite;

namespace Racconotes.Tests
{
    /// <summary>
    /// Smoke-тесты SQLite-стека (gilzoide/unity-sqlite-net). Доказывают, что выбранная
    /// библиотека закрывает требования задания: версия движка ≥ 3.25 (оконные функции §2.4),
    /// работающие LAG/OVER и триггеры (§2.6). Запускаются как EditMode-тесты.
    /// </summary>
    public class SqliteStackSmokeTests
    {
        [Test]
        public void NativeEngine_Loads_And_Version_Supports_WindowFunctions()
        {
            using var conn = new SQLiteConnection(":memory:");
            string version = conn.ExecuteScalar<string>("SELECT sqlite_version();");

            Assert.IsFalse(string.IsNullOrEmpty(version), "sqlite_version() вернул пусто — нативная либа не загрузилась");

            string[] parts = version.Split('.');
            int major = int.Parse(parts[0]);
            int minor = int.Parse(parts[1]);
            bool supportsWindow = major > 3 || (major == 3 && minor >= 25);

            Assert.IsTrue(supportsWindow,
                $"SQLite {version} < 3.25.0 — оконные функции (LAG/OVER) недоступны");
            UnityEngine.Debug.Log($"[smoke] SQLite version = {version}");
        }

        [Test]
        public void WindowFunction_Lag_Over_Executes()
        {
            using var conn = new SQLiteConnection(":memory:");
            conn.Execute("CREATE TABLE results(id INTEGER PRIMARY KEY, accuracy REAL);");
            conn.Execute("INSERT INTO results(accuracy) VALUES (80),(85),(90);");

            // Аналог Запроса 1 из §2.4 (улучшение точности через LAG/OVER).
            List<ImprovementRow> rows = conn.Query<ImprovementRow>(
                "SELECT accuracy, accuracy - LAG(accuracy) OVER (ORDER BY id) AS improvement FROM results ORDER BY id;");

            Assert.AreEqual(3, rows.Count, "Оконный запрос вернул неожиданное число строк");
            Assert.AreEqual(5.0, rows[1].improvement, 1e-6, "LAG посчитал прирост неверно");
        }

        [Test]
        public void Trigger_Fires_On_Insert()
        {
            using var conn = new SQLiteConnection(":memory:");
            conn.Execute("CREATE TABLE src(id INTEGER PRIMARY KEY, v INTEGER);");
            conn.Execute("CREATE TABLE audit(id INTEGER PRIMARY KEY, v INTEGER);");
            conn.Execute(@"CREATE TRIGGER copy_on_insert AFTER INSERT ON src
                           BEGIN
                               INSERT INTO audit(v) VALUES (NEW.v);
                           END;");

            conn.Execute("INSERT INTO src(v) VALUES (42);");

            int audited = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM audit;");
            Assert.AreEqual(1, audited, "Триггер AFTER INSERT не сработал");
        }

        private class ImprovementRow
        {
            public double accuracy { get; set; }
            public double improvement { get; set; }
        }
    }
}
