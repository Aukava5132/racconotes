using System;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;

namespace Racconotes.Application.Scoring
{
    /// <summary>
    /// Накапливает оценки сессии: счётчики, очки, общую точность и комбо (§1.3).
    /// Единственная ответственность — агрегация (SRP). Оценки регистрируются
    /// в порядке нот (по времени), чтобы комбо считалось корректно.
    /// </summary>
    public sealed class ScoreAggregator
    {
        private readonly ScoreModel _scoreModel;

        public ScoreAggregator(ScoreModel scoreModel = null)
        {
            _scoreModel = scoreModel ?? new ScoreModel();
        }

        public int Count300 { get; private set; } // Perfect
        public int Count100 { get; private set; } // Good
        public int Count50 { get; private set; }  // Bad
        public int CountMiss { get; private set; }
        public int TotalScore { get; private set; }
        public int CurrentCombo { get; private set; }
        public int MaxCombo { get; private set; }

        public int TotalNotes => Count300 + Count100 + Count50 + CountMiss;

        /// <summary>
        /// Общая точность = сумма очков / (кол-во нот × 300) × 100 %.
        /// При отсутствии нот возвращает 0 (защита от деления на ноль).
        /// </summary>
        public double AccuracyPercent =>
            TotalNotes == 0 ? 0.0 : (double)TotalScore / (TotalNotes * 300) * 100.0;

        /// <summary>
        /// Зарегистрировать оценку очередной ноты. Комбо растёт на любом попадании
        /// (Perfect/Good/Bad) и обнуляется только на Miss.
        /// </summary>
        public void Register(Judgement judgement)
        {
            switch (judgement)
            {
                case Judgement.Perfect: Count300++; break;
                case Judgement.Good: Count100++; break;
                case Judgement.Bad: Count50++; break;
                default: CountMiss++; break;
            }

            TotalScore += _scoreModel.ScoreFor(judgement);

            if (judgement == Judgement.Miss)
            {
                CurrentCombo = 0;
            }
            else
            {
                CurrentCombo++;
                if (CurrentCombo > MaxCombo) MaxCombo = CurrentCombo;
            }
        }

        /// <summary>Собрать итоговый <see cref="GameResult"/> из накопленных значений.</summary>
        public GameResult BuildResult(int userId, int trackId, DateTime playedAt)
        {
            return new GameResult
            {
                UserId = userId,
                TrackId = trackId,
                PlayedAt = playedAt,
                AccuracyPercent = AccuracyPercent,
                Score300 = Count300,
                Score100 = Count100,
                Score50 = Count50,
                MissCount = CountMiss,
                MaxCombo = MaxCombo
            };
        }
    }
}
