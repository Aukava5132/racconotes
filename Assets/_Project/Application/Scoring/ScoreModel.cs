using System;
using Racconotes.Domain.Enums;

namespace Racconotes.Application.Scoring
{
    /// <summary>
    /// Модель точности (§1.3): по отклонению Δ = |t_u − t_n| определяет оценку и очки.
    /// Чистый сервис без состояния — единственная ответственность судить одно нажатие (SRP).
    /// </summary>
    public sealed class ScoreModel
    {
        private readonly JudgementWindows _windows;

        public ScoreModel(JudgementWindows windows = null)
        {
            _windows = windows ?? JudgementWindows.Default;
        }

        /// <summary>
        /// Оценка по отклонению. Принимает знаковое значение (отрицательное = рано),
        /// сравнение идёт по модулю. Границы по тексту таблицы (включительно слева):
        /// Δ ≤ 50 Perfect, 50 &lt; Δ ≤ 100 Good, 100 &lt; Δ ≤ 200 Bad, иначе Miss.
        /// </summary>
        public Judgement Judge(double deltaMs)
        {
            double abs = Math.Abs(deltaMs);
            if (abs <= _windows.PerfectMs) return Judgement.Perfect;
            if (abs <= _windows.GoodMs) return Judgement.Good;
            if (abs <= _windows.BadMs) return Judgement.Bad;
            return Judgement.Miss;
        }

        /// <summary>Очки за оценку: Perfect 300, Good 100, Bad 50, Miss 0.</summary>
        public int ScoreFor(Judgement judgement)
        {
            switch (judgement)
            {
                case Judgement.Perfect: return 300;
                case Judgement.Good: return 100;
                case Judgement.Bad: return 50;
                default: return 0;
            }
        }

        /// <summary>Композит: оценить отклонение и сразу вернуть очки.</summary>
        public int ScoreForDelta(double deltaMs) => ScoreFor(Judge(deltaMs));

        /// <summary>Попадает ли отклонение в окно засчитываемого нажатия (≤ BadMs).</summary>
        public bool IsWithinHitWindow(double deltaMs) => Math.Abs(deltaMs) <= _windows.BadMs;
    }
}
