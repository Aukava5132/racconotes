using System.Collections.Generic;
using NUnit.Framework;
using Racconotes.Application.Midi;

namespace Racconotes.Tests
{
    /// <summary>Расчёт метаданных трека: плотность нот (нот/сек) и сложность 1..10.</summary>
    public class MidiDifficultyTests
    {
        private static List<RawMidiNote> Notes(int count, double spacing, int baseMidi = 60)
        {
            var list = new List<RawMidiNote>();
            for (int i = 0; i < count; i++)
                list.Add(new RawMidiNote(baseMidi + (i % 12), i * spacing, 0.1));
            return list;
        }

        [Test]
        public void NoteDensity_CountedOverSoundingSpan()
        {
            // 5 нот, последняя начинается на 0.8с и звучит 0.1с → конец трека 0.9с.
            double density = MidiDifficulty.NoteDensity(Notes(5, 0.2));
            Assert.AreEqual(5 / 0.9, density, 1e-6);
        }

        [Test]
        public void NoteDensity_EmptyIsZero()
        {
            Assert.AreEqual(0.0, MidiDifficulty.NoteDensity(new List<RawMidiNote>()));
        }

        [Test]
        public void EstimateDifficulty_WithinRange_AndGrowsWithDensity()
        {
            double sparse = MidiDifficulty.EstimateDifficulty(Notes(5, 1.0));
            double dense = MidiDifficulty.EstimateDifficulty(Notes(50, 0.05));

            Assert.GreaterOrEqual(sparse, 1.0);
            Assert.LessOrEqual(dense, 10.0);
            Assert.Greater(dense, sparse);
        }

        [Test]
        public void EstimateDifficulty_EmptyIsMinimum()
        {
            Assert.AreEqual(1.0, MidiDifficulty.EstimateDifficulty(new List<RawMidiNote>()));
        }
    }
}
