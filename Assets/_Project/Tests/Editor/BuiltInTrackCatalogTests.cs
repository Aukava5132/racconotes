using System.Linq;
using NUnit.Framework;
using Racconotes.Infrastructure.Database;

namespace Racconotes.Tests
{
    /// <summary>
    /// Инварианты встроенного каталога треков (<see cref="BuiltInTrackCatalog"/>): ровно 15 разных
    /// треков, у каждого есть ноты с валидной рукой/пальцем (CHECK схемы), первый — простой tap-only
    /// с ≥6 нотами (на нём держатся демо-прохождения §2.4 и сквозной тест сессии), заголовки уникальны
    /// и не пересекаются с прежним «стандартным» набором.
    /// </summary>
    public class BuiltInTrackCatalogTests
    {
        [Test]
        public void Catalog_HasFifteenTracks()
        {
            Assert.AreEqual(15, BuiltInTrackCatalog.Tracks.Count);
        }

        [Test]
        public void EveryTrack_HasNotes_AndValidMetadata()
        {
            foreach (BuiltInTrackCatalog.BuiltInTrack t in BuiltInTrackCatalog.Tracks)
            {
                Assert.IsNotEmpty(t.Notes, $"У трека «{t.Track.Title}» должны быть ноты.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.Track.Title));
                Assert.Greater(t.Track.Bpm, 0);
                Assert.GreaterOrEqual(t.Track.Difficulty, 1.0);
                Assert.LessOrEqual(t.Track.Difficulty, 10.0);
            }
        }

        [Test]
        public void EveryNote_HasValidHandAndFinger()
        {
            foreach (BuiltInTrackCatalog.BuiltInTrack t in BuiltInTrackCatalog.Tracks)
                foreach (var n in t.Notes)
                {
                    Assert.IsTrue(n.Hand == "left" || n.Hand == "right", $"hand: {n.Hand}");
                    Assert.GreaterOrEqual(n.Finger, 1);
                    Assert.LessOrEqual(n.Finger, 5);
                    Assert.GreaterOrEqual(n.StartTime, 0.0);
                    Assert.Greater(n.Duration, 0.0);
                }
        }

        [Test]
        public void FirstTrack_IsTapOnly_WithAtLeastSixNotes()
        {
            BuiltInTrackCatalog.BuiltInTrack first = BuiltInTrackCatalog.Tracks[0];
            Assert.GreaterOrEqual(first.Notes.Count, 6);
            // tap-only: ни одной длинной ноты (Duration > 0.5 c — порог HoldRules.IsHold).
            Assert.IsFalse(first.Notes.Any(n => n.Duration > 0.5),
                "Первый трек должен быть только тапами (без удержаний).");
        }

        [Test]
        public void Titles_AreUnique_AndDisjointFromLegacy()
        {
            var titles = BuiltInTrackCatalog.Tracks.Select(t => t.Track.Title).ToList();
            Assert.AreEqual(titles.Count, titles.Distinct().Count(), "Заголовки треков должны быть уникальны.");
            foreach (string legacy in BuiltInTrackCatalog.LegacyTitles)
                CollectionAssert.DoesNotContain(titles, legacy,
                    "Новые заголовки не должны совпадать с прежним «стандартным» набором.");
        }
    }
}
