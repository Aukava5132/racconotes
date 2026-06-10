using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Racconotes.Domain.Entities;
using Racconotes.Presentation;

namespace Racconotes.Tests
{
    /// <summary>
    /// Чистая (без рендера) логика слоя Presentation: геометрия клавиатуры, раскладка ввода и
    /// часы песни. MonoBehaviour-рендер и чтение Keyboard.current проверяются ручным прогоном.
    /// </summary>
    public class PresentationPureTests
    {
        // --- PianoLayout ---

        [Test]
        public void PianoLayout_Track1Range_FitsOneOctave_CDtoB()
        {
            // Seed-трек 1 «Ода к радости»: высоты E4/F4/G4 = 64/65/67.
            var layout = new PianoLayout(new[] { 64, 65, 67 });

            Assert.AreEqual(60, layout.LowMidi);   // C4
            Assert.AreEqual(71, layout.HighMidi);  // B4
            Assert.AreEqual(11, layout.HighMidi - layout.LowMidi); // ровно одна октава
            Assert.AreEqual(7, layout.WhiteCount); // C D E F G A B
        }

        [Test]
        public void PianoLayout_ClassifiesBlackKeys()
        {
            Assert.IsFalse(PianoLayout.IsBlack(60)); // C
            Assert.IsTrue(PianoLayout.IsBlack(61));  // C#
            Assert.IsFalse(PianoLayout.IsBlack(62)); // D
            Assert.IsTrue(PianoLayout.IsBlack(66));  // F#
            Assert.IsFalse(PianoLayout.IsBlack(67)); // G
        }

        [Test]
        public void PianoLayout_XForMidi_IsMonotonicAcrossOctave()
        {
            var layout = new PianoLayout(new[] { 60, 71 });
            for (int midi = layout.LowMidi; midi < layout.HighMidi; midi++)
                Assert.Less(layout.XForMidi(midi), layout.XForMidi(midi + 1),
                    $"X должен расти с высотой: {midi} → {midi + 1}");
        }

        [Test]
        public void PianoLayout_BlackKeyWidth_IsNarrowerThanWhite()
        {
            var layout = new PianoLayout(new[] { 60, 71 });
            Assert.Less(layout.WidthForMidi(61), layout.WidthForMidi(60)); // C# уже C
        }

        // --- PianoKeyMap ---

        [Test]
        public void PianoKeyMap_LowerRow_MapsToSemitones()
        {
            Assert.AreEqual(60, PianoKeyMap.MidiFor(Key.Z, 60)); // C4
            Assert.AreEqual(62, PianoKeyMap.MidiFor(Key.X, 60)); // D4 (+2)
            Assert.AreEqual(64, PianoKeyMap.MidiFor(Key.C, 60)); // E4 (+4)
            Assert.AreEqual(65, PianoKeyMap.MidiFor(Key.V, 60)); // F4 (+5)
            Assert.AreEqual(67, PianoKeyMap.MidiFor(Key.B, 60)); // G4 (+7)
        }

        [Test]
        public void PianoKeyMap_UpperRow_ReachesSecondOctave()
        {
            Assert.AreEqual(72, PianoKeyMap.MidiFor(Key.Q, 60)); // C5 (+12)
            Assert.AreEqual(83, PianoKeyMap.MidiFor(Key.U, 60)); // B5 (+23)
        }

        [Test]
        public void PianoKeyMap_UnmappedKey_ReturnsNull()
        {
            Assert.IsNull(PianoKeyMap.MidiFor(Key.Space, 60));
            Assert.IsNull(PianoKeyMap.MidiFor(Key.Enter, 60));
        }

        // --- SongClock ---

        [Test]
        public void SongClock_Start_BeginsWithNegativeLeadIn()
        {
            var clock = new SongClock(leadInSeconds: 2.0);
            clock.Start();
            Assert.Less(clock.Seconds, 0.0);
            Assert.AreEqual(-2000.0, clock.Milliseconds, 1e-9);
        }

        [Test]
        public void SongClock_Milliseconds_IsSecondsTimes1000()
        {
            var clock = new SongClock(leadInSeconds: 0.0);
            clock.Start();
            clock.Tick(1.5f);
            Assert.AreEqual(clock.Seconds * 1000.0, clock.Milliseconds, 1e-9);
        }

        [Test]
        public void SongClock_Tick_AdvancesMonotonically()
        {
            var clock = new SongClock(leadInSeconds: 1.0);
            clock.Start();
            double prev = clock.Seconds;
            foreach (float dt in new[] { 0.1f, 0.2f, 0.3f, 0.4f })
            {
                clock.Tick(dt);
                Assert.Greater(clock.Seconds, prev);
                prev = clock.Seconds;
            }
        }

        [Test]
        public void SongClock_DoesNotAdvance_WhenNotStarted()
        {
            var clock = new SongClock();
            clock.Tick(1.0f); // не запущен
            Assert.AreEqual(0.0, clock.Seconds, 1e-9);
        }

        // --- TrackListing (меню выбора трека) ---

        private static MidiTrack Track(int id, string title, double bpm, double difficulty) =>
            new MidiTrack { TrackId = id, Title = title, Bpm = bpm, Difficulty = difficulty };

        [Test]
        public void TrackListing_Intersect_KeepsOnlyTracksInBothSets()
        {
            // Выборка по BPM (из SQL) и по сложности (из SQL); фильтр «И-И» — только общие TrackId.
            var byBpm = new[] { Track(1, "A", 100, 3), Track(2, "B", 120, 5), Track(3, "C", 140, 7) };
            var byDiff = new[] { Track(2, "B", 120, 5), Track(3, "C", 140, 7), Track(4, "D", 90, 2) };

            var result = TrackListing.Intersect(byBpm, byDiff);

            CollectionAssert.AreEquivalent(new[] { 2, 3 }, result.Select(t => t.TrackId).ToArray());
        }

        [Test]
        public void TrackListing_Intersect_PreservesOrderOfFirstSet()
        {
            var byBpm = new[] { Track(3, "C", 140, 7), Track(1, "A", 100, 3), Track(2, "B", 120, 5) };
            var byDiff = new[] { Track(1, "A", 100, 3), Track(2, "B", 120, 5), Track(3, "C", 140, 7) };

            var result = TrackListing.Intersect(byBpm, byDiff);

            Assert.AreEqual(new[] { 3, 1, 2 }, result.Select(t => t.TrackId).ToArray());
        }

        [Test]
        public void TrackListing_Intersect_DropsDuplicateTrackIds()
        {
            var byBpm = new[] { Track(1, "A", 100, 3), Track(1, "A", 100, 3), Track(2, "B", 120, 5) };
            var byDiff = new[] { Track(1, "A", 100, 3), Track(2, "B", 120, 5) };

            var result = TrackListing.Intersect(byBpm, byDiff);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(new[] { 1, 2 }, result.Select(t => t.TrackId).ToArray());
        }

        [Test]
        public void TrackListing_Sort_ByBpm_Ascending_And_Descending()
        {
            var tracks = new[] { Track(1, "A", 140, 3), Track(2, "B", 100, 5), Track(3, "C", 120, 7) };

            var asc = TrackListing.Sort(tracks, TrackSortKey.Bpm, ascending: true);
            Assert.AreEqual(new[] { 2, 3, 1 }, asc.Select(t => t.TrackId).ToArray()); // 100,120,140

            var desc = TrackListing.Sort(tracks, TrackSortKey.Bpm, ascending: false);
            Assert.AreEqual(new[] { 1, 3, 2 }, desc.Select(t => t.TrackId).ToArray()); // 140,120,100
        }

        [Test]
        public void TrackListing_Sort_ByDifficulty_Ascending()
        {
            var tracks = new[] { Track(1, "A", 140, 7), Track(2, "B", 100, 2), Track(3, "C", 120, 5) };

            var asc = TrackListing.Sort(tracks, TrackSortKey.Difficulty, ascending: true);

            Assert.AreEqual(new[] { 2, 3, 1 }, asc.Select(t => t.TrackId).ToArray()); // 2,5,7
        }

        [Test]
        public void TrackListing_Sort_ByTitle_TieBreaksDeterministically()
        {
            // Равный BPM → детерминированный порядок по названию.
            var tracks = new[] { Track(1, "Гамма", 120, 3), Track(2, "Арпеджио", 120, 5), Track(3, "Блюз", 120, 7) };

            var asc = TrackListing.Sort(tracks, TrackSortKey.Bpm, ascending: true);

            Assert.AreEqual(new[] { 2, 3, 1 }, asc.Select(t => t.TrackId).ToArray()); // Арпеджио, Блюз, Гамма
        }
    }
}
