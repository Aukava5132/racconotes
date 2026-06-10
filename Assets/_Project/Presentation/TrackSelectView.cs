using System;
using System.Collections.Generic;
using UnityEngine;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Repositories;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Экран выбора трека (§1.4 «Фильтрация и поиск»). Рендер через IMGUI (мышь, без TMP).
    /// Фильтр по BPM и сложности выполняет СУБД — <see cref="ITrackRepository.FilterByBpm"/> и
    /// <see cref="ITrackRepository.FilterByDifficulty"/> (реальный SQL: БД активный компонент);
    /// их результаты объединяются в <see cref="TrackListing.Intersect"/> и сортируются
    /// <see cref="TrackListing.Sort"/>. Выбор трека отдаётся наружу через callback.
    /// </summary>
    public sealed class TrackSelectView : MonoBehaviour
    {
        private const int BpmLo = 0, BpmHi = 400, BpmStep = 10;
        private const float DiffLo = 1f, DiffHi = 10f, DiffStep = 1f;

        private ITrackRepository _repo;
        private Action<int> _onChosen;

        private int _minBpm = BpmLo, _maxBpm = BpmHi;
        private float _minDiff = DiffLo, _maxDiff = DiffHi;
        private TrackSortKey _sortKey = TrackSortKey.Title;
        private bool _ascending = true;

        private IReadOnlyList<MidiTrack> _tracks = Array.Empty<MidiTrack>();
        private Vector2 _scroll;

        private GUIStyle _title, _label, _row, _sub, _hint;

        public bool Visible { get; private set; }

        public void Init(ITrackRepository repo, Action<int> onChosen)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _onChosen = onChosen ?? throw new ArgumentNullException(nameof(onChosen));
            Refresh();
        }

        public void Show() { Visible = true; Refresh(); }
        public void Hide() => Visible = false;

        /// <summary>Перевыбрать треки из БД под текущие фильтр и сортировку.</summary>
        private void Refresh()
        {
            if (_repo == null) return;
            IReadOnlyList<MidiTrack> filtered = TrackListing.Intersect(
                _repo.FilterByBpm(_minBpm, _maxBpm),
                _repo.FilterByDifficulty(_minDiff, _maxDiff));
            _tracks = TrackListing.Sort(filtered, _sortKey, _ascending);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            float w = Mathf.Min(720f, Screen.width - 40f);
            float h = Mathf.Min(620f, Screen.height - 40f);
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 24f, box.y + 18f, w - 48f, h - 36f));

            GUILayout.Label("Выбор трека", _title);
            GUILayout.Space(10f);

            bool changed = false;
            _minBpm = IntStepper($"BPM от {_minBpm}", _minBpm, BpmStep, BpmLo, _maxBpm, ref changed);
            _maxBpm = IntStepper($"BPM до {_maxBpm}", _maxBpm, BpmStep, _minBpm, BpmHi, ref changed);
            _minDiff = FloatStepper($"Сложность от {_minDiff:0}", _minDiff, DiffStep, DiffLo, _maxDiff, ref changed);
            _maxDiff = FloatStepper($"Сложность до {_maxDiff:0}", _maxDiff, DiffStep, _minDiff, DiffHi, ref changed);

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Сортировка:", _label, GUILayout.Width(110f));
            if (SortButton("Название", TrackSortKey.Title)) changed = true;
            if (SortButton("BPM", TrackSortKey.Bpm)) changed = true;
            if (SortButton("Сложность", TrackSortKey.Difficulty)) changed = true;
            if (GUILayout.Button(_ascending ? "↑ возр." : "↓ убыв.", GUILayout.Width(90f))) { _ascending = !_ascending; changed = true; }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Найдено треков: {_tracks.Count}", _label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Сбросить фильтр", GUILayout.Width(160f)))
            {
                _minBpm = BpmLo; _maxBpm = BpmHi; _minDiff = DiffLo; _maxDiff = DiffHi;
                changed = true;
            }
            GUILayout.EndHorizontal();

            if (changed) Refresh();

            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_tracks.Count == 0)
                GUILayout.Label("Под фильтр ничего не подошло — измените диапазоны.", _sub);
            foreach (MidiTrack t in _tracks)
                DrawTrackRow(t);
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.Label("Клик «Играть» — запустить трек", _hint);
            GUILayout.EndArea();
        }

        private void DrawTrackRow(MidiTrack t)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.BeginVertical();
            string composer = string.IsNullOrEmpty(t.Composer) ? "" : $" — {t.Composer}";
            GUILayout.Label($"{t.Title}{composer}", _row);
            GUILayout.Label($"BPM {t.Bpm:0}   ·   сложность {t.Difficulty:0.0}   ·   {t.Tonality}   ·   {t.NoteDensity:0.0} нот/с", _sub);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Играть", GUILayout.Width(110f), GUILayout.Height(46f)))
                _onChosen?.Invoke(t.TrackId);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private bool SortButton(string caption, TrackSortKey key)
        {
            bool active = _sortKey == key;
            Color prev = GUI.color;
            if (active) GUI.color = new Color(0.5f, 0.8f, 1f);
            bool clicked = GUILayout.Button(caption, GUILayout.Width(110f));
            GUI.color = prev;
            if (clicked && !active) { _sortKey = key; return true; }
            return false;
        }

        private int IntStepper(string caption, int value, int step, int lo, int hi, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(caption, _label, GUILayout.Width(170f));
            if (GUILayout.Button("−", GUILayout.Width(40f)))
            {
                int v = Mathf.Clamp(value - step, lo, hi);
                if (v != value) { value = v; changed = true; }
            }
            if (GUILayout.Button("+", GUILayout.Width(40f)))
            {
                int v = Mathf.Clamp(value + step, lo, hi);
                if (v != value) { value = v; changed = true; }
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private float FloatStepper(string caption, float value, float step, float lo, float hi, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(caption, _label, GUILayout.Width(170f));
            if (GUILayout.Button("−", GUILayout.Width(40f)))
            {
                float v = Mathf.Clamp(value - step, lo, hi);
                if (!Mathf.Approximately(v, value)) { value = v; changed = true; }
            }
            if (GUILayout.Button("+", GUILayout.Width(40f)))
            {
                float v = Mathf.Clamp(value + step, lo, hi);
                if (!Mathf.Approximately(v, value)) { value = v; changed = true; }
            }
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
