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
        private GameContext _ctx;
        private TrackSelectView _menu;
        private GameplayController _gameplay;

        private void Start()
        {
            if (!GameServices.IsReady)
            {
                Debug.LogError("[Racconotes] GameServices не готов — нет контекста БД. Приложение не запущено.");
                enabled = false;
                return;
            }

            _ctx = GameServices.Context;
            _menu = gameObject.AddComponent<TrackSelectView>();
            _menu.Init(_ctx.TrackRepository, OnTrackChosen);
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

        private void OnTrackChosen(int trackId)
        {
            // Треки без нот играть нечем — остаёмся в меню (защита от пустых записей).
            if (!_ctx.NoteRepository.GetNotesForTrack(trackId).Any())
            {
                Debug.LogWarning($"[Racconotes] Трек {trackId} без нот — выбор проигнорирован.");
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
