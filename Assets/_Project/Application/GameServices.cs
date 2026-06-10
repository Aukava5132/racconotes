namespace Racconotes.Application
{
    /// <summary>
    /// Нейтральная точка доступа к собранному <see cref="GameContext"/>. Живёт в Application —
    /// сборке, которую Presentation уже видит, — чтобы Presentation мог читать контекст,
    /// НЕ ссылаясь на Composition (иначе через Composition транзитивно «протёк» бы Infrastructure).
    ///
    /// Заполняет локатор композиционный корень (push из Composition: <c>GameServices.Set</c>).
    /// Сбрасывается при закрытии БД / выходе из Play Mode (<see cref="Reset"/>) — это важно в
    /// редакторе, где при отключённом domain reload статика переживает Play-сессии.
    /// </summary>
    public static class GameServices
    {
        public static GameContext Context { get; private set; }

        public static bool IsReady => Context != null;

        public static void Set(GameContext context) => Context = context;

        public static void Reset() => Context = null;
    }
}
