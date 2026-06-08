# Архитектура `_Project` (SOLID-слои через asmdef)

Каждая папка — отдельная сборка (`*.asmdef`). Сборка видит только те сборки, на которые
явно ссылается. Поэтому неверная межслойная зависимость превращается в **ошибку компиляции**,
а не в скрытую связь — это и есть машинная проверка слоистой архитектуры (для защиты курсовой).

```
Presentation ─┐
              ├──► Application ──► Domain ◄── Infrastructure
Composition ──┴───────────────────────────────────┘
```

| Слой | asmdef | Ссылается на | Назначение |
|------|--------|--------------|------------|
| **Domain** | `Racconotes.Domain` | — (`noEngineReferences`) | Сущности (`Note`, `MidiTrack`, `GameResult`) и **интерфейсы** репозиториев. Без Unity, без SQLite. |
| **Application** | `Racconotes.Application` | Domain (`noEngineReferences`) | Бизнес-логика: `GameEngine`, FSM (`GameSessionState`), модель точности, `StatsAnalyzer`. Зависит только от интерфейсов Domain. |
| **Infrastructure** | `Racconotes.Infrastructure` | Domain | Реализации: `SQLiteRepo` (реализует интерфейсы Domain), `MidiImporter`. Здесь живут SQLite (gilzoide) и DryWetMIDI. |
| **Presentation** | `Racconotes.Presentation` | Application, Domain | MonoBehaviour'ы, Views, сцены, UI (падающие ноты, ввод). **Не видит** Infrastructure → не может дёрнуть `SQLiteRepo` напрямую. |
| **Composition** | `Racconotes.Composition` | Application, Domain, Infrastructure | Единственное место, где создаётся `SQLiteRepo` и внедряется в бизнес-логику (Dependency Injection / bootstrap). |

## Правило Dependency Inversion (DIP)
asmdef гарантируют **направление** зависимостей, но не сам DIP. DIP обеспечивается тем, что
абстракции (`INoteRepository`, `ITrackRepository`, `IResultRepository`, `IMidiImporter`) объявлены
в **Domain**, реализованы в **Infrastructure**, а конкретный тип подставляется только в **Composition**.
Никакой другой слой не должен ссылаться на Infrastructure.

## Где что писать дальше (по этапам задания)
- **Этап 1 (бизнес-логика):** модели и интерфейсы → Domain; модель точности (Perfect/Good/Bad/Miss),
  FSM, `GameEngine`, `StatsAnalyzer` → Application.
- **Этап 2 (SQLite):** `SQLiteRepo`, схема, запросы, триггеры → Infrastructure; seed-БД → `Assets/StreamingAssets`.
- **Этап 3 (Unity):** сцены, падающие ноты, ввод (Input System), `MidiImporter` UI → Presentation;
  стартовый bootstrap → Composition.

> Все файлы `*.asmdef.meta` коммитим — их GUID'ами слои ссылаются друг на друга.
