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
        private LabelOverlay _overlay;
        private HudView _hud;
        private ResultScreenView _results;
        private ScoreAggregator _aggregator;

        private readonly List<InputEvent> _pressBuffer = new List<InputEvent>();
        private double _endTime;
        private double _missWindowSeconds;
        private bool _finished;

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
            IReadOnlyList<Note> notes = _ctx.Session.BeginPlaying();

            if (notes.Count == 0)
            {
                // Защитная ветка: треки без нот отсеивает AppController до старта, в БД ничего не пишем.
                Debug.LogWarning($"[Racconotes] В треке {trackId} нет нот — возврат в меню.");
                OnExitToMenu?.Invoke();
                return;
            }

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

            // Подписи клавиш/нот по настройкам пользователя (baseMidi = layout.LowMidi, как у ввода).
            // Сбой чтения настроек не должен ронять сессию — иначе часы не стартуют и геймплей зависнет.
            LabelMode keyMode = LabelMode.Off, noteMode = LabelMode.Off;
            try
            {
                UserSettings settings = _ctx.UserSettingsRepository.GetSettings(DefaultUserId);
                keyMode = LabelModeCodec.FromDbString(settings?.KeyLabelMode);
                noteMode = LabelModeCodec.FromDbString(settings?.NoteLabelMode);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Racconotes] Не удалось прочитать настройки подписей: {e.Message}");
            }
            _overlay = gameObject.AddComponent<LabelOverlay>();
            _overlay.Init(layout, layout.LowMidi, HitLineY, _spawner, keyMode, noteMode);

            _hud = gameObject.AddComponent<HudView>();
            _results = gameObject.AddComponent<ResultScreenView>();

            _aggregator = new ScoreAggregator();

            _endTime = 0.0;
            foreach (Note n in notes)
                _endTime = Math.Max(_endTime, n.StartTime + n.Duration);
            _endTime += _missWindowSeconds + 0.6;

            _finished = false;
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

            _clock.Tick(Time.deltaTime);
            double songTime = _clock.Seconds;

            _spawner.UpdateViews(songTime);

            _pressBuffer.Clear();
            _input.CollectPresses(_pressBuffer);
            foreach (InputEvent ev in _pressBuffer)
            {
                _keyboard.FlashPress(ev.MidiNumber);

                HitEvent hit = _ctx.Session.PushInput(ev);
                if (hit != null)
                {
                    _aggregator.Register(hit.Judgement);
                    _keyboard.Flash(ev.MidiNumber, hit.Judgement);
                    _spawner.OnHit(hit.NoteId, hit.Judgement);
                    _hud.Show(_aggregator.CurrentCombo, _aggregator.AccuracyPercent, hit.Judgement);
                }
            }

            if (songTime > _endTime)
                FinishSession();
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
            if (_hud != null) Destroy(_hud);
            if (_results != null) Destroy(_results);
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
