using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Racconotes.Application;
using Racconotes.Application.Engine;
using Racconotes.Application.Scoring;
using Racconotes.Domain.Entities;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Оркестратор игрового экрана (Synthesia-стиль). Берёт <see cref="GameServices.Context"/>,
    /// играет переданный из меню трек (<see cref="Play"/>), запускает realtime-сессию (<c>Session.BeginPlaying</c>),
    /// строит клавиатуру и падающие ноты из кода, кормит нажатия в <c>Session.PushInput</c> для
    /// живой оценки и по концу песни вызывает <c>Session.EndPlaying</c> (сохранение в БД) и показывает
    /// результат. Судейство целиком в Application — здесь только ввод, время и отрисовка.
    /// </summary>
    public sealed class GameplayController : MonoBehaviour
    {
        private const int DefaultUserId = 1;          // seed-демо пользователь
        private const float FallSpeed = 5f;           // мировых единиц в секунду
        private const float WhiteWidth = 1f;
        private const float HitLineY = 0f;
        private const float SpawnLead = 2.2f;         // за сколько секунд до ноты её спавнить
        private const double LeadInSeconds = 2.0;     // «разгон» до первой ноты

        /// <summary>Запрос вернуться в меню выбора трека (нажата M на экране результата).</summary>
        public event Action OnExitToMenu;

        private GameContext _ctx;
        private int _trackId;
        private SongClock _clock;
        private NoteSpawner _spawner;
        private KeyboardInputSource _input;
        private PianoKeyboardView _keyboard;
        private PianoSynth _synth;
        private LabelOverlay _overlay;
        private HudView _hud;
        private ResultScreenView _results;
        private ScoreAggregator _aggregator;

        private PauseMenuView _pause;

        private readonly List<InputEvent> _pressBuffer = new List<InputEvent>();
        private readonly List<int> _releaseBuffer = new List<int>();
        private readonly Dictionary<int, Note> _notesById = new Dictionary<int, Note>();
        private double _endTime;
        private double _missWindowSeconds;
        private bool _finished;
        private bool _paused;
        private float _masterVolume = 1f;

        /// <summary>
        /// Запустить сессию для конкретного трека (его выбрал игрок в меню). Берёт контекст БД из
        /// <see cref="GameServices"/>, строит клавиатуру/ноты под диапазон трека и стартует часы.
        /// </summary>
        public void Play(int trackId)
        {
            if (!GameServices.IsReady)
            {
                Debug.LogError("[Racconotes] GameServices не готов — нет контекста БД. Геймплей не запущен.");
                enabled = false;
                return;
            }

            _ctx = GameServices.Context;
            _trackId = trackId;
            StartSession(trackId);
        }

        private void StartSession(int trackId)
        {
            _ctx.Session.SelectTrack(trackId);
            // BeginPlaying(userId): ноты грузятся с пользовательской аппликатурой (§2.3, COALESCE).
            IReadOnlyList<Note> notes = _ctx.Session.BeginPlaying(DefaultUserId);

            if (notes.Count == 0)
            {
                // Защитная ветка: треки без нот отсеивает AppController до старта, в БД ничего не пишем.
                Debug.LogWarning($"[Racconotes] В треке {trackId} нет нот — возврат в меню.");
                OnExitToMenu?.Invoke();
                return;
            }

            // Карта NoteId → Note: на стороне Presentation решаем «тап/удержание» (HoldRules),
            // не «грязня» Domain-сущность HitEvent.
            _notesById.Clear();
            foreach (Note n in notes) _notesById[n.NoteId] = n;

            var pitches = notes.Select(n => n.MidiNumber).ToList();
            var layout = new PianoLayout(pitches, WhiteWidth);

            SetupCamera(layout);

            _keyboard = NewChild<PianoKeyboardView>("Keyboard");
            _keyboard.Build(layout, HitLineY);

            _missWindowSeconds = JudgementWindows.Default.BadMs / 1000.0;

            _clock = new SongClock(LeadInSeconds);

            _spawner = NewChild<NoteSpawner>("Notes");
            _spawner.Init(notes, layout, FallSpeed, HitLineY, SpawnLead, (float)_missWindowSeconds);

            _input = gameObject.AddComponent<KeyboardInputSource>();
            _input.Init(layout.LowMidi, _clock);

            // Озвучка нот процедурным синтезом под диапазон клавиатуры (без ассетов).
            _synth = gameObject.AddComponent<PianoSynth>();
            _synth.Init(layout.LowMidi, layout.HighMidi);

            // Подписи клавиш/нот по настройкам пользователя (baseMidi = layout.LowMidi, как у ввода).
            // Сбой чтения настроек не должен ронять сессию — иначе часы не стартуют и геймплей зависнет.
            LabelMode keyMode = LabelMode.Off, noteMode = LabelMode.Off;
            try
            {
                UserSettings settings = _ctx.UserSettingsRepository.GetSettings(DefaultUserId);
                keyMode = LabelModeCodec.FromDbString(settings?.KeyLabelMode);
                noteMode = LabelModeCodec.FromDbString(settings?.NoteLabelMode);
                _masterVolume = Mathf.Clamp01((float)(settings?.MasterVolume ?? 1.0));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Racconotes] Не удалось прочитать настройки подписей: {e.Message}");
            }
            _synth.SetVolume(_masterVolume);

            _overlay = gameObject.AddComponent<LabelOverlay>();
            _overlay.Init(layout, layout.LowMidi, HitLineY, _spawner, keyMode, noteMode);

            _hud = gameObject.AddComponent<HudView>();
            _results = gameObject.AddComponent<ResultScreenView>();

            _pause = gameObject.AddComponent<PauseMenuView>();
            _pause.Init(_masterVolume);
            _pause.OnResume += ResumeFromPause;
            _pause.OnExitToMenu += ExitToMenu;
            _pause.OnVolumeChanged += ApplyAndSaveVolume;

            _aggregator = new ScoreAggregator();

            _endTime = 0.0;
            foreach (Note n in notes)
                _endTime = Math.Max(_endTime, n.StartTime + n.Duration);
            _endTime += _missWindowSeconds + 0.6;

            _finished = false;
            _paused = false;
            _clock.Start();

            Debug.Log($"[Racconotes] Старт сессии: трек {trackId}, нот {notes.Count}, диапазон клавиатуры MIDI {layout.LowMidi}..{layout.HighMidi}.");
        }

        private void Update()
        {
            if (_ctx == null) return;

            if (_finished)
            {
                if (_results.Visible && Keyboard.current != null)
                {
                    if (Keyboard.current[Key.R].wasPressedThisFrame)
                    {
                        _results.Hide();
                        Cleanup();
                        StartSession(_trackId); // переиграть тот же трек
                    }
                    else if (Keyboard.current[Key.M].wasPressedThisFrame)
                    {
                        OnExitToMenu?.Invoke(); // вернуться в меню (AppController уничтожит этот объект)
                    }
                }
                return;
            }

            if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
                TogglePause();

            if (_paused) return; // на паузе часы стоят, ввод не принимаем; меню рисует PauseMenuView

            _clock.Tick(Time.deltaTime);
            double songTime = _clock.Seconds;

            _spawner.UpdateViews(songTime);

            _pressBuffer.Clear();
            _input.CollectPresses(_pressBuffer);
            foreach (InputEvent ev in _pressBuffer)
            {
                _keyboard.FlashPress(ev.MidiNumber);
                _synth.Play(ev.MidiNumber); // звук на любое нажатие клавиши, как у инструмента

                HitEvent hit = _ctx.Session.PushInput(ev);
                if (hit == null) continue;

                if (_notesById.TryGetValue(hit.NoteId, out Note n) && HoldRules.IsHold(n.Duration))
                {
                    // Длинная нота: голова нажата — оценка отложена до отпускания/завершения удержания.
                    _keyboard.Flash(ev.MidiNumber, hit.Judgement);
                    _spawner.OnHoldStart(hit.NoteId);
                }
                else
                {
                    RegisterResolved(hit); // обычный тап — оценка сразу
                }
            }

            // Отпускания клавиш: финализируют активные удержания (раннее → Miss, иначе оценка головы).
            _releaseBuffer.Clear();
            _input.CollectReleases(_releaseBuffer);
            foreach (int midi in _releaseBuffer)
            {
                HitEvent resolved = _ctx.Session.PushRelease(midi, _clock.Milliseconds);
                if (resolved != null) RegisterResolved(resolved);
            }

            // Удержания, доведённые до хвоста при зажатой клавише, завершаются по времени.
            foreach (HitEvent resolved in _ctx.Session.TickHolds(_clock.Milliseconds))
                RegisterResolved(resolved);

            if (songTime > _endTime)
                FinishSession();
        }

        /// <summary>Учесть финальную оценку ноты (тап или завершённое удержание): счёт, подсветка, HUD.</summary>
        private void RegisterResolved(HitEvent hit)
        {
            _aggregator.Register(hit.Judgement);
            if (_notesById.TryGetValue(hit.NoteId, out Note n))
                _keyboard.Flash(n.MidiNumber, hit.Judgement);
            _spawner.OnHit(hit.NoteId, hit.Judgement);
            _hud.Show(_aggregator.CurrentCombo, _aggregator.AccuracyPercent, hit.Judgement);
        }

        private void TogglePause()
        {
            if (_paused) { ResumeFromPause(); return; }

            _paused = true;
            _clock.Stop();
            _pause.Init(_masterVolume);
            _pause.Show();
        }

        private void ResumeFromPause()
        {
            _paused = false;
            _pause.Hide();
            _clock.Resume();
        }

        private void ExitToMenu()
        {
            _ctx.Session.AbortPlaying(); // выход из паузы без сохранения результата
            OnExitToMenu?.Invoke();
        }

        private void ApplyAndSaveVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            if (_synth != null) _synth.SetVolume(_masterVolume);
            try
            {
                UserSettings s = _ctx.UserSettingsRepository.GetSettings(DefaultUserId)
                                 ?? new UserSettings { UserId = DefaultUserId };
                s.MasterVolume = _masterVolume;
                _ctx.UserSettingsRepository.SaveSettings(s);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Racconotes] Не удалось сохранить громкость: {e.Message}");
            }
        }

        private void FinishSession()
        {
            _finished = true;
            _clock.Stop();

            SessionEvaluation eval = _ctx.Session.EndPlaying(DefaultUserId, DateTime.Now);
            _hud.SetAccuracy(eval.Result.AccuracyPercent);
            _results.ShowResult(eval.Result);

            Debug.Log($"[Racconotes] Сессия завершена: точность {eval.Result.AccuracyPercent:0.0}%, " +
                      $"макс. комбо {eval.Result.MaxCombo}. Результат сохранён в БД (ResultId={eval.Result.ResultId}).");
        }

        private void Cleanup()
        {
            if (_keyboard != null) Destroy(_keyboard.gameObject);
            if (_spawner != null) Destroy(_spawner.gameObject);
            if (_overlay != null) Destroy(_overlay);
            if (_input != null) Destroy(_input);
            if (_synth != null) Destroy(_synth);
            if (_hud != null) Destroy(_hud);
            if (_results != null) Destroy(_results);
            if (_pause != null) Destroy(_pause);
        }

        private T NewChild<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private void SetupCamera(PianoLayout layout)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.10f);

            float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            float topY = HitLineY + SpawnLead * FallSpeed + 2f;
            float bottomY = HitLineY - 4f;
            float halfHeight = (topY - bottomY) / 2f;
            float centerY = (topY + bottomY) / 2f;
            float halfForWidth = (layout.TotalWidth / 2f + 0.5f) / aspect;

            cam.orthographicSize = Mathf.Max(halfHeight, halfForWidth);
            cam.transform.position = new Vector3(0f, centerY, -10f);
        }
    }
}
