using UnityEngine;
using Racconotes.Domain.Enums;

namespace Racconotes.Presentation
{
    /// <summary>Цвета и подписи оценок для нот, клавиш и HUD — единый стиль судейства.</summary>
    public static class JudgementVisuals
    {
        public static Color Tint(Judgement j) => j switch
        {
            Judgement.Perfect => new Color(0.20f, 1.00f, 0.45f),
            Judgement.Good    => new Color(1.00f, 0.95f, 0.30f),
            Judgement.Bad     => new Color(1.00f, 0.60f, 0.20f),
            _                 => new Color(0.55f, 0.55f, 0.60f),
        };

        public static string Label(Judgement j) => j switch
        {
            Judgement.Perfect => "PERFECT",
            Judgement.Good    => "GOOD",
            Judgement.Bad     => "BAD",
            _                 => "MISS",
        };
    }
}
