namespace Racconotes.Presentation
{
    /// <summary>
    /// Время песни в секундах и миллисекундах. Стартует с отрицательного «разгона» (lead-in),
    /// чтобы первые ноты успели «упасть» сверху до линии попадания. Чистый C# — продвигается
    /// извне через <see cref="Tick"/> (в Unity — на <c>Time.deltaTime</c>), тестируется в EditMode.
    /// </summary>
    public sealed class SongClock
    {
        private readonly double _leadInSeconds;

        public double Seconds { get; private set; }
        public bool Running { get; private set; }

        public SongClock(double leadInSeconds = 2.0)
        {
            _leadInSeconds = leadInSeconds;
        }

        /// <summary>Время песни в мс — единица для <c>InputEvent.TimeMs</c> и судейства.</summary>
        public double Milliseconds => Seconds * 1000.0;

        /// <summary>Запустить отсчёт с отрицательного lead-in.</summary>
        public void Start()
        {
            Seconds = -_leadInSeconds;
            Running = true;
        }

        /// <summary>Продвинуть время на прошедшие секунды (если запущен).</summary>
        public void Tick(float deltaSeconds)
        {
            if (Running) Seconds += deltaSeconds;
        }

        public void Stop() => Running = false;
    }
}
