using System.Linq;
using UnityEngine;
using Racconotes.Application;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Координатор экранов рантайма: меню выбора трека ↔ игровой экран. Создаётся
    /// <see cref="PresentationBootstrap"/> после <c>RuntimeBootstrap</c> (БД уже открыта и
    /// опубликована в <see cref="GameServices"/>). Меню отдаёт выбранный трек; по концу песни
    /// <see cref="GameplayController"/> просит вернуться в меню (событие OnExitToMenu).
    /// </summary>
    public sealed class AppController : MonoBehaviour
    {
        private const int DefaultUserId = 1; // демо-пользователь (как в GameplayController)

        private GameContext _ctx;
        private TrackSelectView _menu;
        private StatsView _stats;
        private SettingsView _settings;
        private FingerEditorView _fingers;
        private GameplayController _gameplay;

        private void Start()
        {
            if (!GameServices.IsReady)
            {
                Debug.LogError("[Racconotes] GameServices не готовий — немає контексту БД. Застосунок не запущено.");
                enabled = false;
                return;
            }

            _ctx = GameServices.Context;
            _menu = gameObject.AddComponent<TrackSelectView>();
            _menu.Init(_ctx, DefaultUserId, OnTrackChosen, ShowStats, ShowSettings, ShowFingerEditor);

            _stats = gameObject.AddComponent<StatsView>();
            _stats.Init(_ctx, DefaultUserId);
            _stats.Hide();
            _stats.OnBack += BackFromStats;

            _settings = gameObject.AddComponent<SettingsView>();
            _settings.Init(_ctx, DefaultUserId);
            _settings.Hide();
            _settings.OnBack += BackFromSettings;
            _settings.OnLibraryChanged += () => _menu.Refresh();

            _fingers = gameObject.AddComponent<FingerEditorView>();
            _fingers.Init(_ctx, DefaultUserId);
            _fingers.Hide();
            _fingers.OnBack += BackFromFingerEditor;

            ShowMenu();
        }

        private void ShowMenu()
        {
            if (_gameplay != null)
            {
                _gameplay.OnExitToMenu -= ShowMenu;
                Destroy(_gameplay.gameObject);
                _gameplay = null;
            }

            _menu.Show();
        }

        private void ShowStats()
        {
            _menu.Hide();
            _stats.Show();
        }

        private void BackFromStats()
        {
            _stats.Hide();
            ShowMenu();
        }

        private void ShowSettings()
        {
            _menu.Hide();
            _settings.Show();
        }

        private void BackFromSettings()
        {
            _settings.Hide();
            ShowMenu();
        }

        private void ShowFingerEditor()
        {
            _menu.Hide();
            _fingers.Show();
        }

        private void BackFromFingerEditor()
        {
            _fingers.Hide();
            ShowMenu();
        }

        private void OnTrackChosen(int trackId)
        {
            // Треки без нот играть нечем — остаёмся в меню (защита от пустых записей).
            if (!_ctx.NoteRepository.GetNotesForTrack(trackId).Any())
            {
                Debug.LogWarning($"[Racconotes] Трек {trackId} без нот — вибір проігноровано.");
                return;
            }

            _menu.Hide();

            var go = new GameObject("Gameplay");
            go.transform.SetParent(transform, false);
            _gameplay = go.AddComponent<GameplayController>();
            _gameplay.OnExitToMenu += ShowMenu;
            _gameplay.Play(trackId);
        }
    }
}
