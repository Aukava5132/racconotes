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
            return hit;
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
