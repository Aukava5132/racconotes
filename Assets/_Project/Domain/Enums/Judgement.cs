namespace Racconotes.Domain.Enums
{
    /// <summary>
    /// Оценка точности нажатия (модель Osu! из §1.3 задания).
    /// Очки за каждую оценку задаёт <c>ScoreModel</c> в слое Application,
    /// поэтому сам enum не несёт числовых значений (разделение ответственности).
    /// </summary>
    public enum Judgement
    {
        Perfect, // Δ ≤ 50 мс  → 300 очков
        Good,    // 50 < Δ ≤ 100 мс → 100 очков
        Bad,     // 100 < Δ ≤ 200 мс → 50 очков
        Miss     // Δ > 200 мс или нет нажатия → 0 очков
    }
}
