using System;
using System.Linq;
using NUnit.Framework;
using Racconotes.Application.Midi;

namespace Racconotes.Tests
{
    /// <summary>
    /// Парсер SMF: эталонный (закодированный вручную) файл как независимая проверка корректности,
    /// плюс сгенерированные через <see cref="TestMidi"/> сценарии — running status, смена темпа,
    /// тональность, Note On velocity 0 как Note Off, и осмысленные исключения на битых данных.
    /// </summary>
    public class SmfParserTests
    {
        // Эталон, проверенный побайтно: формат 0, PPQ=480, Set-Tempo 500000 мкс (120 BPM),
        // одна нота C4(60) длиной в четверть = 0.5 с при 120 BPM.
        private static readonly byte[] ReferenceMidi =
        {
            0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x01, 0x01, 0xE0, // MThd
            0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00, 0x14,                                     // MTrk, len=20
            0x00, 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20, // delta0: Set-Tempo 0x07A120 = 500000
            0x00, 0x90, 0x3C, 0x64,                   // delta0: Note On  ch0 note60 vel100
            0x83, 0x60, 0x80, 0x3C, 0x40,             // delta480: Note Off ch0 note60 vel64
            0x00, 0xFF, 0x2F, 0x00                    // delta0: End of Track
        };

        [Test]
        public void Parse_ReferenceFile_OneNote_120Bpm()
        {
            MidiParseResult result = SmfParser.Parse(ReferenceMidi);

            Assert.AreEqual(120.0, result.Bpm, 1e-6);
            Assert.AreEqual(480, result.TicksPerQuarterNote);
            Assert.IsNull(result.Tonality);
            Assert.AreEqual(1, result.Notes.Count);

            RawMidiNote note = result.Notes[0];
            Assert.AreEqual(60, note.MidiNumber);
            Assert.AreEqual(0.0, note.StartSeconds, 1e-6);
            Assert.AreEqual(0.5, note.DurationSeconds, 1e-6);
        }

        [Test]
        public void Parse_TwoNotes_SortedByStart_WithTiming()
        {
            byte[] data = TestMidi.Format0(480,
                TestMidi.Tempo(0, 500_000),
                TestMidi.NoteOn(0, 0, 60, 100),
                TestMidi.NoteOff(480, 0, 60, 0),
                TestMidi.NoteOn(0, 0, 62, 100),
                TestMidi.NoteOff(240, 0, 62, 0),
                TestMidi.EndOfTrack(0));

            MidiParseResult result = SmfParser.Parse(data);

            Assert.AreEqual(2, result.Notes.Count);
            Assert.AreEqual(60, result.Notes[0].MidiNumber);
            Assert.AreEqual(0.0, result.Notes[0].StartSeconds, 1e-6);
            Assert.AreEqual(0.5, result.Notes[0].DurationSeconds, 1e-6);
            Assert.AreEqual(62, result.Notes[1].MidiNumber);
            Assert.AreEqual(0.5, result.Notes[1].StartSeconds, 1e-6);
            Assert.AreEqual(0.25, result.Notes[1].DurationSeconds, 1e-6);
        }

        [Test]
        public void Parse_RunningStatus_KeepsTwoNotes()
        {
            byte[] data = TestMidi.Format0(480,
                TestMidi.NoteOn(0, 0, 60, 100),  // задаёт running status 0x90
                TestMidi.Event(0, 0x3E, 0x64),   // без статус-байта → Note On note62 vel100
                TestMidi.NoteOff(480, 0, 60, 0),
                TestMidi.NoteOff(0, 0, 62, 0),
                TestMidi.EndOfTrack(0));

            MidiParseResult result = SmfParser.Parse(data);

            CollectionAssert.AreEquivalent(new[] { 60, 62 }, result.Notes.Select(n => n.MidiNumber).ToList());
        }

        [Test]
        public void Parse_NoteOnZeroVelocity_TreatedAsNoteOff()
        {
            byte[] data = TestMidi.Format0(480,
                TestMidi.NoteOn(0, 0, 64, 100),
                TestMidi.NoteOn(480, 0, 64, 0),  // velocity 0 = Note Off
                TestMidi.EndOfTrack(0));

            MidiParseResult result = SmfParser.Parse(data);

            Assert.AreEqual(1, result.Notes.Count);
            Assert.AreEqual(0.5, result.Notes[0].DurationSeconds, 1e-6); // 120 BPM по умолчанию
        }

        [Test]
        public void Parse_TempoChange_AffectsLaterNotes()
        {
            byte[] data = TestMidi.Format0(480,
                TestMidi.Tempo(0, 500_000),      // 120 BPM
                TestMidi.NoteOn(0, 0, 60, 100),
                TestMidi.NoteOff(480, 0, 60, 0), // нота 1: 0.0 .. 0.5
                TestMidi.Tempo(0, 250_000),      // 240 BPM на тике 480
                TestMidi.NoteOn(0, 0, 62, 100),
                TestMidi.NoteOff(480, 0, 62, 0), // нота 2: 0.5 .. 0.75
                TestMidi.EndOfTrack(0));

            MidiParseResult result = SmfParser.Parse(data);

            Assert.AreEqual(120.0, result.Bpm, 1e-6); // BPM трека = первый темп
            Assert.AreEqual(0.5, result.Notes[1].StartSeconds, 1e-6);
            Assert.AreEqual(0.25, result.Notes[1].DurationSeconds, 1e-6);
        }

        [TestCase(0, 0, "C")]
        [TestCase(0, 1, "Am")]
        [TestCase(1, 0, "G")]
        [TestCase(-1, 0, "F")]
        public void Parse_KeySignature_MapsToTonality(int sf, int mi, string expected)
        {
            byte[] data = TestMidi.Format0(480,
                TestMidi.KeySig(0, sf, mi),
                TestMidi.NoteOn(0, 0, 60, 100),
                TestMidi.NoteOff(480, 0, 60, 0),
                TestMidi.EndOfTrack(0));

            Assert.AreEqual(expected, SmfParser.Parse(data).Tonality);
        }

        [Test]
        public void Parse_Null_Throws() =>
            Assert.Throws<ArgumentNullException>(() => SmfParser.Parse(null));

        [Test]
        public void Parse_BadHeader_Throws() =>
            Assert.Throws<FormatException>(() => SmfParser.Parse(new byte[] { 0, 1, 2, 3, 4, 5 }));

        [Test]
        public void Parse_Format2_NotSupported()
        {
            byte[] header = { 0x4D, 0x54, 0x68, 0x64, 0, 0, 0, 6, 0, 2, 0, 1, 1, 0xE0 };
            Assert.Throws<NotSupportedException>(() => SmfParser.Parse(header));
        }
    }
}
