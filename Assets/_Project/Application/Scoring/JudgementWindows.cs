namespace Racconotes.Application.Scoring
{
    /// <summary>
    /// Пороги судейства в миллисекундах (модель Osu!, §1.3).
    /// Вынесены в отдельный объект ради OCP: новые наборы окон (сложности, режимы)
    /// добавляются без изменения логики <see cref="ScoreModel"/>.
    /// </summary>
    public sealed class JudgementWindows
    {
        public double PerfectMs { get; }
        public double GoodMs { get; }
        public double BadMs { get; }

        public JudgementWindows(double perfectMs = 50, double goodMs = 100, double badMs = 200)
        {
            PerfectMs = perfectMs;
            GoodMs = goodMs;
            BadMs = badMs;
        }

        /// <summary>Стандартные окна из задания: 50 / 100 / 200 мс.</summary>
        public static JudgementWindows Default { get; } = new JudgementWindows();

        /// <summary>
        /// Смягчённые окна для живого геймплея (90 / 160 / 300 мс): дают «свободу по задержкам» —
        /// нажатие засчитывается в пределах ±300 мс, что заметно прощает неточный тайминг на клавиатуре
        /// и мыши. Намеренно отдельный набор, чтобы не трогать <see cref="Default"/> (на нём держится
        /// доказательная модель точности §1.3 и её тесты).
        /// </summary>
        public static JudgementWindows Relaxed { get; } = new JudgementWindows(perfectMs: 90, goodMs: 160, badMs: 300);
    }
}
