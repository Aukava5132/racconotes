namespace Racconotes.Application
{
    /// <summary>
    /// Конечный автомат игровой сессии (§1.3 задания):
    /// Idle → Selecting → Loading → Playing → Evaluating → SavingResult → Idle.
    /// </summary>
    public enum GameSessionState
    {
        Idle,
        Selecting,
        Loading,
        Playing,
        Evaluating,
        SavingResult
    }
}
