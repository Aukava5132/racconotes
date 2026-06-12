using System;
using Racconotes.Application.Engine;
using Racconotes.Application.Midi;
using Racconotes.Application.Stats;
using Racconotes.Domain.Repositories;

namespace Racconotes.Application
{
    /// <summary>
    /// Готовый граф зависимостей рантайма, который композиционный корень (слой Composition)
    /// собирает один раз при запуске и публикует через <see cref="GameServices"/>.
    ///
    /// Экспонирует ТОЛЬКО абстракции Domain (интерфейсы репозиториев) и сервисы Application —
    /// никаких SQLite-типов. Поэтому слой Presentation может потреблять контекст, видя лишь
    /// Application+Domain, и ничего не зная про Infrastructure (DIP сохранён).
    ///
    /// <see cref="Engine.GameEngine"/> наружу не выносим — он инкапсулирован в <see cref="Session"/>.
    /// </summary>
    public sealed class GameContext
    {
        public ITrackRepository TrackRepository { get; }
        public INoteRepository NoteRepository { get; }
        public IResultRepository ResultRepository { get; }
        public IStatsQueries StatsQueries { get; }

        /// <summary>Настройки пользователя (режимы подписи и т.д.) — таблица UserSettings как активный компонент.</summary>
        public IUserSettingsRepository UserSettingsRepository { get; }

        /// <summary>Пользовательская аппликатура (§2.3) — переопределения руки/пальца в UserFingerAssignments.</summary>
        public IUserFingerAssignmentRepository FingerAssignments { get; }

        /// <summary>Главный оркестратор игровой сессии (FSM §1.3) для Presentation.</summary>
        public SessionController Session { get; }

        /// <summary>Импорт MIDI → БД (управление библиотекой, §1.4).</summary>
        public MidiImportService MidiImport { get; }

        /// <summary>In-memory аналитика поверх результатов сессии (§2.4).</summary>
        public StatsAnalyzer StatsAnalyzer { get; }

        public GameContext(
            ITrackRepository trackRepository,
            INoteRepository noteRepository,
            IResultRepository resultRepository,
            IStatsQueries statsQueries,
            IUserSettingsRepository userSettingsRepository,
            IUserFingerAssignmentRepository fingerAssignments,
            SessionController session,
            MidiImportService midiImport,
            StatsAnalyzer statsAnalyzer)
        {
            TrackRepository = trackRepository ?? throw new ArgumentNullException(nameof(trackRepository));
            NoteRepository = noteRepository ?? throw new ArgumentNullException(nameof(noteRepository));
            ResultRepository = resultRepository ?? throw new ArgumentNullException(nameof(resultRepository));
            StatsQueries = statsQueries ?? throw new ArgumentNullException(nameof(statsQueries));
            UserSettingsRepository = userSettingsRepository ?? throw new ArgumentNullException(nameof(userSettingsRepository));
            FingerAssignments = fingerAssignments ?? throw new ArgumentNullException(nameof(fingerAssignments));
            Session = session ?? throw new ArgumentNullException(nameof(session));
            MidiImport = midiImport ?? throw new ArgumentNullException(nameof(midiImport));
            StatsAnalyzer = statsAnalyzer ?? throw new ArgumentNullException(nameof(statsAnalyzer));
        }
    }
}
