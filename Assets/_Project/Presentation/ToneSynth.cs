using System;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Процедурный синтез тона ноты по MIDI-номеру (без аудио-ассетов, в духе проекта). Чистый C#
    /// (возвращает массив сэмплов) — тестируется в EditMode; обёртка в AudioClip и воспроизведение —
    /// в <see cref="PianoSynth"/>. Частота равнотемперированного строя: f = 440·2^((midi−69)/12).
    /// Тембр — основной тон + 2-3 гармоники; огибающая — мгновенная атака и экспоненциальное
    /// затухание (грубая имитация фортепиано).
    /// </summary>
    public static class ToneSynth
    {
        /// <summary>Частота ноты в Гц (A4 = MIDI 69 = 440 Гц).</summary>
        public static double Frequency(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        /// <summary>
        /// Сгенерировать моно-сэмплы тона в диапазоне [-1..1]. <paramref name="seconds"/> — длительность,
        /// <paramref name="sampleRate"/> — частота дискретизации (обычно 44100).
        /// </summary>
        public static float[] Render(int midi, int sampleRate, float seconds)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));

            int count = (int)(sampleRate * seconds);
            var data = new float[count];

            double twoPiF = 2.0 * Math.PI * Frequency(midi);
            const double decay = 4.5;       // больше — быстрее затухание
            const double level = 0.6;       // общий уровень громкости
            const double norm = 1.0 + 0.5 + 0.25; // сумма амплитуд гармоник для нормировки

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Exp(-decay * t);
                double harmonics =
                    1.00 * Math.Sin(twoPiF * t) +
                    0.50 * Math.Sin(2.0 * twoPiF * t) +
                    0.25 * Math.Sin(3.0 * twoPiF * t);
                data[i] = (float)(harmonics / norm * env * level);
            }
            return data;
        }
    }
}
