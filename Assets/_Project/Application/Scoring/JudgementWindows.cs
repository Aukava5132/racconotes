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
    }
}
