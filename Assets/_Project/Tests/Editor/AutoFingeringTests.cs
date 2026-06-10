using NUnit.Framework;
using Racconotes.Application.Midi;
using Racconotes.Domain.Enums;

namespace Racconotes.Tests
{
    /// <summary>Автоаппликатура: выбор руки по порогу C4(60) и палец всегда в допустимом [1..5].</summary>
    public class AutoFingeringTests
    {
        [TestCase(36, Hand.Left)]
        [TestCase(48, Hand.Left)]
        [TestCase(59, Hand.Left)]
        [TestCase(60, Hand.Right)]
        [TestCase(72, Hand.Right)]
        [TestCase(96, Hand.Right)]
        public void Assign_PicksHandBySplit(int midi, Hand expected)
        {
            Assert.AreEqual(expected, AutoFingering.Assign(midi).Hand);
        }

        [Test]
        public void Assign_FingerAlwaysInValidRange()
        {
            for (int midi = 21; midi <= 108; midi++) // диапазон 88-клавишного пианино
            {
                int finger = AutoFingering.Assign(midi).Finger;
                Assert.GreaterOrEqual(finger, 1, $"midi={midi}");
                Assert.LessOrEqual(finger, 5, $"midi={midi}");
            }
        }
    }
}
