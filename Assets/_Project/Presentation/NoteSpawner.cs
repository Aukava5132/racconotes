using System.Collections.Generic;
using UnityEngine;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Управляет вью падающих нот: спавнит по мере приближения времени, двигает каждый кадр,
    /// помечает визуальный промах для пропущенных и убирает ушедшие за линию. Авторитетный
    /// подсчёт промахов делает GameEngine на EndPlaying — здесь только визуал.
    /// </summary>
    public sealed class NoteSpawner : MonoBehaviour
    {
        private const float CullAfterSeconds = 0.6f;

        private PianoLayout _layout;
        private float _fallSpeed;
        private float _hitLineY;
        private float _spawnLeadSeconds;
        private float _missWindowSeconds;

        private List<Note> _notes;
        private int _nextIndex;
        private readonly Dictionary<int, NoteView> _live = new Dictionary<int, NoteView>();

        public int LiveCount => _live.Count;

        /// <summary>Живые вью нот на экране (для наложения текстовых подписей в <see cref="LabelOverlay"/>).</summary>
        public IEnumerable<NoteView> LiveViews => _live.Values;

        public void Init(IReadOnlyList<Note> notes, PianoLayout layout, float fallSpeed,
            float hitLineY, float spawnLeadSeconds, float missWindowSeconds)
        {
            _layout = layout;
            _fallSpeed = fallSpeed;
            _hitLineY = hitLineY;
            _spawnLeadSeconds = spawnLeadSeconds;
            _missWindowSeconds = missWindowSeconds;

            _notes = new List<Note>(notes);
            _notes.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            _nextIndex = 0;
        }

        public void UpdateViews(double songTime)
        {
            // Спавн приближающихся нот.
            while (_nextIndex < _notes.Count && _notes[_nextIndex].StartTime - songTime <= _spawnLeadSeconds)
            {
                Note n = _notes[_nextIndex++];
                if (_live.ContainsKey(n.NoteId)) continue;

                var go = new GameObject($"Note{n.NoteId}");
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<NoteView>();
                view.Init(n, _layout, _fallSpeed, _hitLineY);
                _live[n.NoteId] = view;
            }

            // Двигаем, помечаем промахи, собираем на удаление.
            List<int> done = null;
            foreach (KeyValuePair<int, NoteView> kv in _live)
            {
                NoteView view = kv.Value;
                view.SetSongTime(songTime);

                double past = songTime - view.Model.StartTime;
                if (!view.Consumed && past > _missWindowSeconds)
                    view.MarkJudged(Judgement.Miss);

                if (past > _missWindowSeconds + CullAfterSeconds)
                    (done ??= new List<int>()).Add(kv.Key);
            }

            if (done != null)
                foreach (int id in done)
                {
                    _live[id].Despawn();
                    _live.Remove(id);
                }
        }

        /// <summary>Перекрасить попавшую ноту в цвет оценки.</summary>
        public void OnHit(int noteId, Judgement judgement)
        {
            if (_live.TryGetValue(noteId, out NoteView view))
                view.MarkJudged(judgement);
        }
    }
}
