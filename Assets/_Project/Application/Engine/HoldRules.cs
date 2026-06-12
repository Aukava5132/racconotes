namespace Racconotes.Application.Engine
{
    /// <summary>
    /// Правило различения обычной ноты-тапа и длинной ноты-удержания (long note, osu!mania):
    /// нота считается удержанием, если её длительность превышает порог. Единый источник правды
    /// для движка (судейство головы/хвоста) и отрисовки (визуал бара) — порог легко настраивается.
    /// </summary>
    public static class HoldRules
    {
        /// <summary>Минимальная длительность (сек), при превышении которой нота — удержание.</summary>
        public const double MinDurationSeconds = 0.5;

        /// <summary>Удержание ли это (нота длиннее порога) — иначе обычный тап.</summary>
        public static bool IsHold(double durationSeconds) => durationSeconds > MinDurationSeconds;
    }
}
