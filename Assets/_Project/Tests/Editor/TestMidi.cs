using System.Collections.Generic;
using System.Linq;

namespace Racconotes.Tests
{
    /// <summary>
    /// Конструктор байтов Standard MIDI File (формат 0) для тестов парсера и импорта.
    /// Каждый хелпер-событие возвращает delta-time (VLQ) + тело события; <see cref="Format0"/>
    /// собирает из них валидный MThd + MTrk.
    /// </summary>
    internal static class TestMidi
    {
        public static byte[] Vlq(int value)
        {
            var stack = new Stack<byte>();
            stack.Push((byte)(value & 0x7F));
            value >>= 7;
            while (value > 0)
            {
                stack.Push((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            return stack.ToArray();
        }

        public static byte[] Event(int delta, params byte[] body)
        {
            var bytes = new List<byte>();
            bytes.AddRange(Vlq(delta));
            bytes.AddRange(body);
            return bytes.ToArray();
        }

        public static byte[] Tempo(int delta, int micros) =>
            Event(delta, 0xFF, 0x51, 0x03, (byte)(micros >> 16), (byte)(micros >> 8), (byte)micros);

        public static byte[] KeySig(int delta, int sf, int mi) =>
            Event(delta, 0xFF, 0x59, 0x02, (byte)(sbyte)sf, (byte)mi);

        public static byte[] NoteOn(int delta, int channel, int note, int velocity) =>
            Event(delta, (byte)(0x90 | channel), (byte)note, (byte)velocity);

        public static byte[] NoteOff(int delta, int channel, int note, int velocity) =>
            Event(delta, (byte)(0x80 | channel), (byte)note, (byte)velocity);

        public static byte[] EndOfTrack(int delta) => Event(delta, 0xFF, 0x2F, 0x00);

        /// <summary>Собирает MIDI формата 0 из одного трека (последовательность событий).</summary>
        public static byte[] Format0(int ppq, params byte[][] events)
        {
            byte[] track = events.SelectMany(e => e).ToArray();
            var bytes = new List<byte>();
            bytes.AddRange(new byte[] { 0x4D, 0x54, 0x68, 0x64 }); // "MThd"
            bytes.AddRange(UInt32BE(6));
            bytes.AddRange(UInt16BE(0)); // формат 0
            bytes.AddRange(UInt16BE(1)); // один трек
            bytes.AddRange(UInt16BE(ppq));
            bytes.AddRange(new byte[] { 0x4D, 0x54, 0x72, 0x6B }); // "MTrk"
            bytes.AddRange(UInt32BE(track.Length));
            bytes.AddRange(track);
            return bytes.ToArray();
        }

        private static byte[] UInt32BE(int v) => new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
        private static byte[] UInt16BE(int v) => new byte[] { (byte)(v >> 8), (byte)v };
    }
}
