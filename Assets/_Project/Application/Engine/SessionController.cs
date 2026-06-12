using System;
using System.Collections.Generic;
using System.Linq;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Repositories;

namespace Racconotes.Application.Engine
{
    /// <summary>
    /// Оркестратор игровой сессии: реализует полный FSM §1.3
    /// Idle → Selecting → Loading → Playing → Evaluating → SavingResult → Idle.
    ///
    /// Зависит только от абстракций Domain (<see cref="INoteRepository"/>,
    /// <see cref="IResultRepository"/>) — конкретные реализации (SQLite) подставит слой
    /// Composition (DIP). Загрузку нот и сохранение результата делает контроллер, а само
    /// сопоставление — <see cref="GameEngine"/> (SRP).
    /// </summary>
    public sealed class SessionController
    {
        private readonly INoteRepository _noteRepo;
        private readonly IResultRepository _resultRepo;
        private readonly GameEngine _engine;

        public GameSessionState State { get; private set; } = GameSessionState.Idle;
        public int? SelectedTrackId { get; private set; }

        public SessionController(INoteRepository noteRepo, IResultRepository resultRepo, GameEngine engine = null)
        {
            _noteRepo = noteRepo ?? throw new ArgumentNullException(nameof(noteRepo));
            _resultRepo = resultRepo ?? throw new ArgumentNullException(nameof(resultRepo));
            _engine = engine ?? new GameEngine();
        }

        /// <summary>Выбрать трек для игры: Idle → Selecting.</summary>
        public void SelectTrack(int trackId)
        {
            State = GameSessionState.Selecting;
            SelectedTrackId = trackId;
        }

        /// <summary>
        /// Сыграть выбранный трек целиком: загрузить ноты из БД (Loading), прогнать движок
        /// (Playing → Evaluating), сохранить результат (SavingResult) и вернуться в Idle.
        /// </summary>
        public SessionEvaluation PlaySelectedTrack(
            int userId, IReadOnlyList<InputEvent> inputs, DateTime playedAt)
        {
            if (State != GameSessionState.Selecting || SelectedTrackId == null)
                throw new InvalidOperationException("Сначала выберите трек через SelectTrack.");

            int trackId = SelectedTrackId.Value;

            State = GameSessionState.Loading;
            List<Note> notes = _noteRepo.GetNotesForTrack(trackId).ToList();

            // GameEngine внутри проходит Playing → Evaluating.
            SessionEvaluation eval = _engine.Run(notes, inputs, userId, trackId, playedAt);
            State = GameSessionState.Evaluating;

            State = GameSessionState.SavingResult;
            PersistEvaluation(eval);

            State = GameSessionState.Idle;
            SelectedTrackId = null;
            return eval;
        }

        /// <summary>
        /// Realtime-старт сессии (Этап 3, Unity): загрузить ноты из БД (Loading) и перевести
        /// движок в Playing. Возвращает загруженные ноты (StartTime в секундах) — слой Presentation
        /// рендерит ими падающие ноты и строит диапазон клавиатуры. Затем подавать нажатия через
        /// <see cref="PushInput"/> и завершить <see cref="EndPlaying"/>.
        /// </summary>
        public IReadOnlyList<Note> BeginPlaying()
        {
            EnsureSelecting();
            State = GameSessionState.Loading;
            return StartEngine(_noteRepo.GetNotesForTrack(SelectedTrackId.Value).ToList());
        }

        /// <summary>
        /// Как <see cref="BeginPlaying()"/>, но ноты грузятся с применённой пользовательской
        /// аппликатурой (§2.3): рука/палец берутся из UserFingerAssignments (COALESCE поверх Notes).
        /// Судейство аппликатуру не использует — влияет только на отображение/анализ.
        /// </summary>
        public IReadOnlyList<Note> BeginPlaying(int userId)
        {
            EnsureSelecting();
            State = GameSessionState.Loading;
            return StartEngine(_noteRepo.GetNotesForTrack(SelectedTrackId.Value, userId).ToList());
        }

        private void EnsureSelecting()
        {
            if (State != GameSessionState.Selecting || SelectedTrackId == null)
                throw new InvalidOperationException("Сначала выберите трек через SelectTrack.");
        }

        private IReadOnlyList<Note> StartEngine(List<Note> notes)
        {
            _engine.Begin(notes); // внутри Loading → Playing
            State = GameSessionState.Playing;
            return notes;
        }

        /// <summary>
        /// Подать одно нажатие в активную realtime-сессию (живая обратная связь). Проксирует
        /// <see cref="GameEngine.OnInput"/>: возвращает <see cref="HitEvent"/> с оценкой либо null,
        /// если в окне нет подходящей ноты (лишнее нажатие). Судейство целиком в Application —
        /// Presentation его не дублирует.
        /// </summary>
        public HitEvent PushInput(InputEvent input)
        {
            if (State != GameSessionState.Playing)
                throw new InvalidOperationException("PushInput допустим только во время игры (после BeginPlaying).");

            return _engine.OnInput(input);
        }

        /// <summary>
        /// Подать отпускание клавиши (для длинных нот-удержаний). Проксирует
        /// <see cref="GameEngine.OnRelease"/>: возвращает финализированный <see cref="HitEvent"/>
        /// удержания (оценка головы либо Miss при раннем отпускании) либо null, если активного
        /// удержания этой высоты нет.
        /// </summary>
        public HitEvent PushRelease(int midiNumber, double timeMs)
        {
            if (State != GameSessionState.Playing)
                throw new InvalidOperationException("PushRelease допустим только во время игры (после BeginPlaying).");

            return _engine.OnRelease(midiNumber, timeMs);
        }

        /// <summary>
        /// Продвинуть активные удержания во времени: те, что доведены до конца (хвост прошёл линию),
        /// финализируются как успех. Проксирует <see cref="GameEngine.TickHolds"/>. Вызывается каждый
        /// кадр со временем песни в мс.
        /// </summary>
        public IReadOnlyList<HitEvent> TickHolds(double nowMs)
        {
            if (State != GameSessionState.Playing)
                throw new InvalidOperationException("TickHolds допустим только во время игры (после BeginPlaying).");

            return _engine.TickHolds(nowMs);
        }

        /// <summary>
        /// Прервать активную realtime-сессию без сохранения результата (выход в меню из паузы):
        /// сбросить FSM в Idle. Идемпотентно — вне Playing ничего не делает.
        /// </summary>
        public void AbortPlaying()
        {
            if (State != GameSessionState.Playing) return;
            State = GameSessionState.Idle;
            SelectedTrackId = null;
        }

        /// <summary>
        /// Завершить realtime-сессию: ненажатые ноты → Miss, агрегировать результат (Evaluating),
        /// сохранить <see cref="GameResult"/> и <see cref="HitEvent"/>-ы в БД (SavingResult) и
        /// вернуться в Idle. Возвращает итоговую <see cref="SessionEvaluation"/>.
        /// </summary>
        public SessionEvaluation EndPlaying(int userId, DateTime playedAt)
        {
            if (State != GameSessionState.Playing)
                throw new InvalidOperationException("EndPlaying без активной игры: сначала BeginPlaying.");

            int trackId = SelectedTrackId.Value;

            State = GameSessionState.Evaluating;
            SessionEvaluation eval = _engine.Finish(userId, trackId, playedAt);

            State = GameSessionState.SavingResult;
            PersistEvaluation(eval);

            State = GameSessionState.Idle;
            SelectedTrackId = null;
            return eval;
        }

        /// <summary>
        /// Сохранить результат сессии: сначала <see cref="GameResult"/> (репозиторий проставит
        /// ResultId через last_insert_rowid), затем связать с ним нажатия и сохранить
        /// <see cref="HitEvent"/>-ы — нужно для аналитики слабых мест/задержек (§2.4).
        /// </summary>
        private void PersistEvaluation(SessionEvaluation eval)
        {
            _resultRepo.SaveGameResult(eval.Result);
            foreach (HitEvent hit in eval.HitEvents)
                hit.ResultId = eval.Result.ResultId;
            _resultRepo.SaveHitEvents(eval.HitEvents);
        }
    }
}
