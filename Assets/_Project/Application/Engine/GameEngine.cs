using System;
using System.Collections.Generic;
using System.Linq;
using Racconotes.Application.Scoring;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;

namespace Racconotes.Application.Engine
{
    /// <summary>
    /// Ядро игрового цикла (§1.3): сопоставляет нажатия с ожидаемыми нотами, порождает
    /// <see cref="HitEvent"/>-ы и продвигает FSM (Loading → Playing → Evaluating).
    ///
    /// Работает с переданными данными, а не с репозиториями (DIP): загрузку нот и сохранение
    /// результата делает <c>SessionController</c> уровнем выше.
    ///
    /// Два режима поверх одного алгоритма:
    /// • <see cref="Run"/> — пакетный, детерминированный (для тестов и оффлайн-проверки);
    /// • <see cref="Begin"/>/<see cref="OnInput"/>/<see cref="Finish"/> — пошаговый (для realtime в Unity, Этап 3).
    /// </summary>
    public sealed class GameEngine
    {
        private readonly ScoreModel _scoreModel;
        private readonly JudgementWindows _windows;

        private List<Note> _notes;     // отсортированы по времени
        private double[] _expectedMs;  // ожидаемое время нажатия каждой ноты в мс
        private HitEvent[] _hits;      // назначенное нажатие на ноту; null = ещё не попали

        // Активные удержания (длинные ноты): MIDI → (индекс ноты, время конца удержания в мс).
        private readonly Dictionary<int, (int idx, double releaseMs)> _activeHolds =
            new Dictionary<int, (int, double)>();
        private readonly List<HitEvent> _resolvedHolds = new List<HitEvent>(); // буфер возврата TickHolds

        public GameSessionState State { get; private set; } = GameSessionState.Idle;

        public GameEngine(ScoreModel scoreModel = null, JudgementWindows windows = null)
        {
            _windows = windows ?? JudgementWindows.Default;
            _scoreModel = scoreModel ?? new ScoreModel(_windows);
        }

        /// <summary>Пакетный прогон: загрузка нот, подача всех вводов, оценка — за один вызов.</summary>
        public SessionEvaluation Run(
            IReadOnlyList<Note> notes,
            IReadOnlyList<InputEvent> inputs,
            int userId,
            int trackId,
            DateTime playedAt)
        {
            Begin(notes);
            foreach (InputEvent input in inputs.OrderBy(i => i.TimeMs))
                OnInput(input);
            return Finish(userId, trackId, playedAt);
        }

        /// <summary>Загрузить ноты и перейти в Playing. Время нот переводится из секунд в мс.</summary>
        public void Begin(IReadOnlyList<Note> notes)
        {
            if (notes == null) throw new ArgumentNullException(nameof(notes));

            State = GameSessionState.Loading;
            _notes = notes.OrderBy(n => n.StartTime).ThenBy(n => n.NoteIndex).ToList();
            _expectedMs = _notes.Select(n => n.StartTime * 1000.0).ToArray();
            _hits = new HitEvent[_notes.Count];
            _activeHolds.Clear();
            State = GameSessionState.Playing;
        }

        /// <summary>
        /// Обработать одно нажатие. Сопоставляет с ближайшей ненажатой нотой той же высоты
        /// в окне ≤ BadMs. Возвращает созданный <see cref="HitEvent"/> либо null, если ноты
        /// в окне нет (лишнее нажатие игнорируется).
        /// </summary>
        public HitEvent OnInput(InputEvent input)
        {
            if (State != GameSessionState.Playing)
                throw new InvalidOperationException("OnInput допустим только в состоянии Playing (после Begin).");

            int best = -1;
            double bestAbs = double.MaxValue;

            // Ноты отсортированы по времени, поэтому при равном |Δ| первой выбирается более ранняя.
            for (int i = 0; i < _notes.Count; i++)
            {
                if (_hits[i] != null) continue;
                if (_notes[i].MidiNumber != input.MidiNumber) continue;

                double abs = Math.Abs(input.TimeMs - _expectedMs[i]);
                if (abs > _windows.BadMs) continue;

                if (abs < bestAbs)
                {
                    bestAbs = abs;
                    best = i;
                }
            }

            if (best < 0) return null; // лишнее нажатие

            double delta = input.TimeMs - _expectedMs[best];
            var hit = new HitEvent
            {
                NoteId = _notes[best].NoteId,
                ExpectedTime = _expectedMs[best],
                ActualTime = input.TimeMs,
                DeltaMs = (int)Math.Round(delta),
                Judgement = _scoreModel.Judge(delta),
                FingerUsed = input.FingerUsed
            };
            _hits[best] = hit;

            // Длинная нота: зарегистрировать активное удержание до момента отпускания/завершения.
            if (HoldRules.IsHold(_notes[best].Duration))
                _activeHolds[input.MidiNumber] = (best, _expectedMs[best] + _notes[best].Duration * 1000.0);

            return hit;
        }

        /// <summary>
        /// Отпускание клавиши для активного удержания (длинной ноты). Если отпустили заметно раньше
        /// конца ноты (за пределами окна BadMs) — удержание сорвано: оценка ноты → Miss. Иначе
        /// оценка головы сохраняется. Возвращает финализированный <see cref="HitEvent"/> либо null,
        /// если активного удержания этой высоты нет (обычный тап или лишнее отпускание).
        /// </summary>
        public HitEvent OnRelease(int midiNumber, double timeMs)
        {
            if (State != GameSessionState.Playing)
                throw new InvalidOperationException("OnRelease допустим только в состоянии Playing (после Begin).");

            if (!_activeHolds.TryGetValue(midiNumber, out (int idx, double releaseMs) hold))
                return null;

            _activeHolds.Remove(midiNumber);

            HitEvent hit = _hits[hold.idx];
            if (timeMs < hold.releaseMs - _windows.BadMs)
                hit.Judgement = Judgement.Miss; // отпустили слишком рано — удержание не засчитано

            return hit;
        }

        /// <summary>
        /// Продвинуть активные удержания во времени песни (мс): те, у которых хвост уже прошёл линию
        /// (nowMs ≥ конец + BadMs) при всё ещё зажатой клавише — успешно завершить (оценка головы
        /// сохраняется). Возвращает завершившиеся в этом тике удержания (общий переиспользуемый буфер).
        /// </summary>
        public IReadOnlyList<HitEvent> TickHolds(double nowMs)
        {
            if (State != GameSessionState.Playing)
                throw new InvalidOperationException("TickHolds допустим только в состоянии Playing (после Begin).");

            _resolvedHolds.Clear();
            if (_activeHolds.Count == 0) return _resolvedHolds;

            List<int> done = null;
            foreach (KeyValuePair<int, (int idx, double releaseMs)> kv in _activeHolds)
            {
                if (nowMs < kv.Value.releaseMs + _windows.BadMs) continue;
                _resolvedHolds.Add(_hits[kv.Value.idx]);
                (done ??= new List<int>()).Add(kv.Key);
            }

            if (done != null)
                foreach (int midi in done) _activeHolds.Remove(midi);

            return _resolvedHolds;
        }

        /// <summary>
        /// Завершить сессию: ненажатые ноты → Miss, оценки агрегируются в порядке нот
        /// (для корректного комбо). Переводит FSM в Evaluating.
        /// </summary>
        public SessionEvaluation Finish(int userId, int trackId, DateTime playedAt)
        {
            if (_notes == null)
                throw new InvalidOperationException("Finish без Begin: сессия не загружена.");

            State = GameSessionState.Evaluating;

            var aggregator = new ScoreAggregator(_scoreModel);
            var ordered = new List<HitEvent>(_notes.Count);

            for (int i = 0; i < _notes.Count; i++)
            {
                HitEvent hit = _hits[i] ?? new HitEvent
                {
                    NoteId = _notes[i].NoteId,
                    ExpectedTime = _expectedMs[i],
                    ActualTime = double.NaN, // ноту не нажали
                    DeltaMs = 0,
                    Judgement = Judgement.Miss,
                    FingerUsed = 0
                };

                aggregator.Register(hit.Judgement);
                ordered.Add(hit);
            }

            GameResult result = aggregator.BuildResult(userId, trackId, playedAt);
            return new SessionEvaluation(ordered, result);
        }
    }
}
