using System;
using System.Collections.Generic;

namespace Racconotes.Application.Midi
{
    /// <summary>
    /// Минимальный парсер Standard MIDI File (SMF, форматы 0 и 1) на чистом C# — без внешних
    /// зависимостей. Извлекает ноты (Note On/Off → начало и длительность в секундах), темп и
    /// тональность. Реализует переменную длину (VLQ), running status, карту темпа и перевод
    /// тиков в секунды. Формат 2 и SMPTE-деление не поддерживаются (осознанно — игре не нужны).
    /// </summary>
    public static class SmfParser
    {
        private const int DefaultMicrosPerQuarter = 500_000; // 120 BPM

        /// <summary>Разбирает байты MIDI-файла. Бросает <see cref="FormatException"/> на битых данных.</summary>
        public static MidiParseResult Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            int pos = 0;
            ReadChunkId(data, ref pos, "MThd");
            int headerLen = (int)ReadUInt32(data, ref pos);
            if (headerLen < 6) throw new FormatException("MThd: некорректная длина заголовка.");
            int format = ReadUInt16(data, ref pos);
            int trackCount = ReadUInt16(data, ref pos);
            int division = ReadUInt16(data, ref pos);
            pos += headerLen - 6; // пропустить возможные лишние байты заголовка

            if (format == 2)
                throw new NotSupportedException("MIDI формат 2 (независимые последовательности) не поддерживается.");
            if ((division & 0x8000) != 0)
                throw new NotSupportedException("SMPTE-деление времени не поддерживается, нужен PPQ.");
            if (division <= 0)
                throw new FormatException("MThd: division (тиков на четверть) должен быть положительным.");

            int ppq = division;

            var noteSpans = new List<(long Start, long End, int Note)>();
            var tempos = new List<(long Tick, int Micros)>();
            long firstKeyTick = long.MaxValue;
            string tonality = null;

            for (int t = 0; t < trackCount; t++)
            {
                ReadChunkId(data, ref pos, "MTrk");
                int trackLen = (int)ReadUInt32(data, ref pos);
                int trackEnd = pos + trackLen;
                if (trackEnd > data.Length) throw new FormatException("MTrk: длина трека выходит за пределы файла.");

                ParseTrack(data, ref pos, trackEnd, noteSpans, tempos,
                    ref firstKeyTick, ref tonality);

                pos = trackEnd; // защита от рассинхронизации внутри трека
            }

            int firstMicros = DefaultMicrosPerQuarter;
            long firstTempoTick = long.MaxValue;
            foreach (var tempo in tempos)
                if (tempo.Tick < firstTempoTick) { firstTempoTick = tempo.Tick; firstMicros = tempo.Micros; }
            double bpm = 60_000_000.0 / firstMicros;

            var tempoMap = new TempoMap(tempos, ppq);

            var notes = new List<RawMidiNote>(noteSpans.Count);
            foreach (var span in noteSpans)
            {
                double start = tempoMap.TickToSeconds(span.Start);
                double end = tempoMap.TickToSeconds(span.End);
                notes.Add(new RawMidiNote(span.Note, start, Math.Max(0.0, end - start)));
            }
            notes.Sort((a, b) => a.StartSeconds.CompareTo(b.StartSeconds));

            return new MidiParseResult(notes, bpm, tonality, ppq);
        }

        private static void ParseTrack(
            byte[] data, ref int pos, int trackEnd,
            List<(long Start, long End, int Note)> noteSpans,
            List<(long Tick, int Micros)> tempos,
            ref long firstKeyTick, ref string tonality)
        {
            long tick = 0;
            int runningStatus = 0;
            // (channel<<8 | note) -> очередь тиков начала (FIFO), на случай повторных Note On той же высоты.
            var open = new Dictionary<int, Queue<long>>();

            while (pos < trackEnd)
            {
                tick += ReadVlq(data, ref pos);
                int status = data[pos];

                if (status < 0x80)
                {
                    // running status: статус повторяется, текущий байт — уже данные.
                    if (runningStatus == 0) throw new FormatException("Running status без предшествующего статуса.");
                    status = runningStatus;
                }
                else
                {
                    pos++;
                }

                if (status == 0xFF) // meta-событие
                {
                    runningStatus = 0;
                    int type = data[pos++];
                    int len = ReadVlq(data, ref pos);
                    int dataStart = pos;

                    if (type == 0x51 && len == 3) // Set Tempo
                    {
                        int micros = (data[dataStart] << 16) | (data[dataStart + 1] << 8) | data[dataStart + 2];
                        tempos.Add((tick, micros));
                    }
                    else if (type == 0x59 && len == 2) // Key Signature
                    {
                        if (tick < firstKeyTick)
                        {
                            firstKeyTick = tick;
                            tonality = KeySignatureToTonality((sbyte)data[dataStart], data[dataStart + 1]);
                        }
                    }
                    // 0x2F End of Track и прочие meta — просто пропускаем по длине.
                    pos = dataStart + len;
                }
                else if (status == 0xF0 || status == 0xF7) // SysEx
                {
                    runningStatus = 0;
                    int len = ReadVlq(data, ref pos);
                    pos += len;
                }
                else // канальное голосовое сообщение
                {
                    runningStatus = status;
                    int type = status & 0xF0;
                    int channel = status & 0x0F;

                    if (type == 0x90) // Note On
                    {
                        int note = data[pos++];
                        int velocity = data[pos++];
                        if (velocity > 0)
                        {
                            int key = (channel << 8) | note;
                            if (!open.TryGetValue(key, out Queue<long> q)) { q = new Queue<long>(); open[key] = q; }
                            q.Enqueue(tick);
                        }
                        else
                        {
                            CloseNote(open, channel, note, tick, noteSpans);
                        }
                    }
                    else if (type == 0x80) // Note Off
                    {
                        int note = data[pos++];
                        pos++; // velocity (игнорируем)
                        CloseNote(open, channel, note, tick, noteSpans);
                    }
                    else if (type == 0xC0 || type == 0xD0) // Program Change / Channel Pressure — 1 байт данных
                    {
                        pos += 1;
                    }
                    else // 0xA0 / 0xB0 / 0xE0 — 2 байта данных
                    {
                        pos += 2;
                    }
                }
            }

            // Незакрытые ноты в конце трека закрываем последним тиком трека.
            foreach (KeyValuePair<int, Queue<long>> kv in open)
            {
                int note = kv.Key & 0xFF;
                while (kv.Value.Count > 0)
                    noteSpans.Add((kv.Value.Dequeue(), tick, note));
            }
        }

        private static void CloseNote(
            Dictionary<int, Queue<long>> open, int channel, int note, long tick,
            List<(long Start, long End, int Note)> noteSpans)
        {
            int key = (channel << 8) | note;
            if (open.TryGetValue(key, out Queue<long> q) && q.Count > 0)
                noteSpans.Add((q.Dequeue(), tick, note));
        }

        // --- Карта темпа: накопленные секунды на границах темпа для быстрого перевода тиков ---

        private sealed class TempoMap
        {
            private readonly long[] _ticks;
            private readonly double[] _secPerTick;
            private readonly double[] _cumSeconds;

            public TempoMap(List<(long Tick, int Micros)> tempos, int ppq)
            {
                // Отсортировать по тику, дедуплицировать (на одном тике побеждает последний).
                var sorted = new List<(long Tick, int Micros)>(tempos);
                sorted.Sort((a, b) => a.Tick.CompareTo(b.Tick));

                var ticks = new List<long>();
                var micros = new List<int>();
                foreach (var tempo in sorted)
                {
                    if (ticks.Count > 0 && ticks[ticks.Count - 1] == tempo.Tick)
                        micros[micros.Count - 1] = tempo.Micros;
                    else { ticks.Add(tempo.Tick); micros.Add(tempo.Micros); }
                }
                // Гарантировать сегмент с тика 0 (до первого темпа действует темп по умолчанию).
                if (ticks.Count == 0 || ticks[0] != 0)
                {
                    ticks.Insert(0, 0);
                    micros.Insert(0, DefaultMicrosPerQuarter);
                }

                _ticks = ticks.ToArray();
                _secPerTick = new double[_ticks.Length];
                _cumSeconds = new double[_ticks.Length];
                for (int i = 0; i < _ticks.Length; i++)
                    _secPerTick[i] = (micros[i] / 1_000_000.0) / ppq;
                for (int i = 1; i < _ticks.Length; i++)
                    _cumSeconds[i] = _cumSeconds[i - 1] + (_ticks[i] - _ticks[i - 1]) * _secPerTick[i - 1];
            }

            public double TickToSeconds(long tick)
            {
                int i = UpperSegment(tick);
                return _cumSeconds[i] + (tick - _ticks[i]) * _secPerTick[i];
            }

            private int UpperSegment(long tick)
            {
                int lo = 0, hi = _ticks.Length - 1, result = 0;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (_ticks[mid] <= tick) { result = mid; lo = mid + 1; }
                    else hi = mid - 1;
                }
                return result;
            }
        }

        // --- Низкоуровневое чтение ---

        private static void ReadChunkId(byte[] data, ref int pos, string expected)
        {
            if (pos + 4 > data.Length) throw new FormatException($"Ожидался чанк '{expected}', данные закончились.");
            for (int i = 0; i < 4; i++)
                if (data[pos + i] != (byte)expected[i])
                    throw new FormatException($"Ожидался чанк '{expected}' на позиции {pos}.");
            pos += 4;
        }

        private static uint ReadUInt32(byte[] data, ref int pos)
        {
            if (pos + 4 > data.Length) throw new FormatException("Неожиданный конец данных (uint32).");
            uint value = (uint)((data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3]);
            pos += 4;
            return value;
        }

        private static int ReadUInt16(byte[] data, ref int pos)
        {
            if (pos + 2 > data.Length) throw new FormatException("Неожиданный конец данных (uint16).");
            int value = (data[pos] << 8) | data[pos + 1];
            pos += 2;
            return value;
        }

        private static int ReadVlq(byte[] data, ref int pos)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                if (pos >= data.Length) throw new FormatException("Неожиданный конец данных (VLQ).");
                byte b = data[pos++];
                value = (value << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) return value;
            }
            throw new FormatException("VLQ длиннее 4 байт.");
        }

        private static readonly string[] MajorKeys =
            { "Cb", "Gb", "Db", "Ab", "Eb", "Bb", "F", "C", "G", "D", "A", "E", "B", "F#", "C#" };

        private static readonly string[] MinorKeys =
            { "Abm", "Ebm", "Bbm", "Fm", "Cm", "Gm", "Dm", "Am", "Em", "Bm", "F#m", "C#m", "G#m", "D#m", "A#m" };

        private static string KeySignatureToTonality(sbyte sharpsFlats, int minor)
        {
            int index = sharpsFlats + 7;
            if (index < 0 || index > 14) return null;
            return minor == 1 ? MinorKeys[index] : MajorKeys[index];
        }
    }
}
