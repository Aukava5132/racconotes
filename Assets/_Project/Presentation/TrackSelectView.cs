using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Racconotes.Application;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Stats;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Экран выбора трека (§1.4 «Фильтрация и поиск») в osu!-образной раскладке: шапка с выбранным
    /// треком сверху, прокручиваемый список треков со ★-рейтингом справа, компактная сводка
    /// статистики слева (на месте «локального топа» osu!), фильтр/сортировка снизу. Рендер — чистый
    /// IMGUI (мышь, без TMP и ассетов): панели рисуются <c>GUI.DrawTexture</c> + <c>GUI.color</c>.
    ///
    /// Фильтр по BPM и сложности выполняет СУБД — <see cref="Racconotes.Domain.Repositories.ITrackRepository.FilterByBpm"/>
    /// и <c>FilterByDifficulty</c> (реальный SQL: БД активный компонент); их результаты объединяются
    /// в <see cref="TrackListing.Intersect"/> и сортируются <see cref="TrackListing.Sort"/>.
    /// Левая сводка читает все 4 аналитических запроса §2.4 через <see cref="GameContext.StatsQueries"/>;
    /// «Подробнее» открывает полный <see cref="StatsView"/>. Выбор трека отдаётся наружу через callback.
    /// </summary>
    public sealed class TrackSelectView : MonoBehaviour
    {
        private const int BpmLo = 0, BpmHi = 400, BpmStep = 10;
        private const float DiffLo = 1f, DiffHi = 10f, DiffStep = 1f;

        private static readonly Color Backdrop = new Color(0.04f, 0.05f, 0.09f, 1f);
        private static readonly Color PanelBg = new Color(0.09f, 0.11f, 0.16f, 0.92f);
        private static readonly Color RowBg = new Color(0.12f, 0.14f, 0.20f, 0.92f);
        private static readonly Color RowSel = new Color(0.15f, 0.30f, 0.48f, 0.95f);
        private static readonly Color Accent = new Color(0.30f, 0.65f, 1f);
        private static readonly Color AccentDim = new Color(0.30f, 0.65f, 1f, 0.35f);

        private GameContext _ctx;
        private int _userId;
        private Action<int> _onChosen;
        private Action _onShowStats;
        private Action _onShowSettings;
        private Action _onShowFingerEditor;

        private int _minBpm = BpmLo, _maxBpm = BpmHi;
        private float _minDiff = DiffLo, _maxDiff = DiffHi;
        private TrackSortKey _sortKey = TrackSortKey.Title;
        private bool _ascending = true;

        private IReadOnlyList<MidiTrack> _tracks = Array.Empty<MidiTrack>();
        private int _selectedTrackId = -1;
        private Vector2 _scroll;

        // Кэш сводки статистики — заполняется в LoadStats(), а не в OnGUI (SQL не дёргаем каждый кадр).
        private IReadOnlyList<AccuracyPoint> _progress = Array.Empty<AccuracyPoint>();
        private IReadOnlyList<WeakSpot> _weak = Array.Empty<WeakSpot>();
        private IReadOnlyList<TrackRecommendation> _recommend = Array.Empty<TrackRecommendation>();
        private IReadOnlyList<LatencyByFinger> _latency = Array.Empty<LatencyByFinger>();

        private GUIStyle _title, _label, _row, _sub, _section, _statTitle, _starsL, _starsR, _bigPlay;

        public bool Visible { get; private set; }

        public void Init(GameContext ctx, int userId, Action<int> onChosen, Action onShowStats,
            Action onShowSettings, Action onShowFingerEditor)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _userId = userId;
            _onChosen = onChosen ?? throw new ArgumentNullException(nameof(onChosen));
            _onShowStats = onShowStats ?? throw new ArgumentNullException(nameof(onShowStats));
            _onShowSettings = onShowSettings ?? throw new ArgumentNullException(nameof(onShowSettings));
            _onShowFingerEditor = onShowFingerEditor ?? throw new ArgumentNullException(nameof(onShowFingerEditor));
            Refresh();
        }

        public void Show() { Visible = true; Refresh(); }
        public void Hide() => Visible = false;

        /// <summary>Перевыбрать треки из БД под текущие фильтр и сортировку; пере-разрешить выбор и сводку.</summary>
        public void Refresh()
        {
            if (_ctx == null) return;
            IReadOnlyList<MidiTrack> filtered = TrackListing.Intersect(
                _ctx.TrackRepository.FilterByBpm(_minBpm, _maxBpm),
                _ctx.TrackRepository.FilterByDifficulty(_minDiff, _maxDiff));
            _tracks = TrackListing.Sort(filtered, _sortKey, _ascending);

            if (_tracks.Count == 0)
                _selectedTrackId = -1;
            else if (_tracks.All(t => t.TrackId != _selectedTrackId))
                _selectedTrackId = _tracks[0].TrackId;

            LoadStats();
        }

        /// <summary>Перечитать сводку статистики из БД (глобальные запросы + точность по выбранному треку).</summary>
        private void LoadStats()
        {
            if (_ctx == null) return;
            _weak = _ctx.StatsQueries.GetWeakSpots(_userId, 3);
            _recommend = _ctx.StatsQueries.RecommendTracks(_userId);
            _latency = _ctx.StatsQueries.GetAverageLatency(_userId);
            _progress = _selectedTrackId > 0
                ? _ctx.StatsQueries.GetAccuracyProgress(_userId, _selectedTrackId)
                : Array.Empty<AccuracyPoint>();
        }

        private MidiTrack Selected()
        {
            foreach (MidiTrack t in _tracks)
                if (t.TrackId == _selectedTrackId) return t;
            return null;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _section = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _statTitle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };

            var gold = new Color(1f, 0.82f, 0.28f);
            _starsL = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            _starsL.normal.textColor = gold;
            _starsR = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleRight };
            _starsR.normal.textColor = gold;

            _bigPlay = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            Fill(new Rect(0f, 0f, Screen.width, Screen.height), Backdrop);

            const float pad = 16f, gap = 10f, headerH = 104f, bottomH = 92f, leftW = 330f;
            var header = new Rect(pad, pad, Screen.width - 2f * pad, headerH);
            var bottom = new Rect(pad, Screen.height - pad - bottomH, Screen.width - 2f * pad, bottomH);
            float contentY = header.yMax + gap;
            float contentH = bottom.y - gap - contentY;
            var left = new Rect(pad, contentY, leftW, contentH);
            var right = new Rect(left.xMax + gap, contentY, Screen.width - pad - (left.xMax + gap), contentH);

            DrawHeader(header);
            DrawStatsPanel(left);
            DrawTrackList(right);
            DrawFilterBar(bottom);
        }

        private void DrawHeader(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 10f, r.width - 32f, r.height - 20f));
            MidiTrack sel = Selected();
            if (sel == null)
            {
                GUILayout.Label("Під фільтр нічого не підійшло — змініть діапазони нижче.", _row);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                string composer = string.IsNullOrEmpty(sel.Composer) ? "" : $" — {sel.Composer}";
                GUILayout.Label($"{sel.Title}{composer}", _title);
                GUILayout.Label(
                    $"BPM {sel.Bpm:0}   ·   {sel.Tonality}   ·   складність {sel.Difficulty:0.0}   ·   {sel.NoteDensity:0.0} нот/с",
                    _sub);
                GUILayout.Label(Stars(sel.Difficulty), _starsL);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("► Грати", _bigPlay, GUILayout.Width(170f), GUILayout.Height(68f)))
                    _onChosen?.Invoke(sel.TrackId);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void DrawStatsPanel(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 12f, r.width - 32f, r.height - 24f));

            GUILayout.Label("СТАТИСТИКА", _statTitle);
            GUILayout.Space(8f);

            GUILayout.Label("Точність за треком", _section);
            if (_progress.Count == 0)
            {
                GUILayout.Label("Ще не грали", _sub);
            }
            else
            {
                AccuracyPoint last = _progress[_progress.Count - 1];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{last.Accuracy:0.0}%", _row, GUILayout.Width(90f));
                DrawImprovement(last.Improvement);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(12f);

            GUILayout.Label("Слабкі ноти", _section);
            if (_weak.Count == 0)
                GUILayout.Label("Промахів немає", _sub);
            else
                foreach (WeakSpot s in _weak)
                    GUILayout.Label($"{NoteNaming.Name(s.MidiNumber)}  ·  {HandRu(s.Hand)}  ·  ×{s.MissCount}", _sub);
            GUILayout.Space(12f);

            GUILayout.Label("Рекомендуємо", _section);
            GUILayout.Label(_recommend.Count == 0 ? "—" : _recommend[0].Title, _sub);
            GUILayout.Space(12f);

            GUILayout.Label("Затримка", _section);
            if (_latency.Count == 0)
                GUILayout.Label("немає даних", _sub);
            else
                for (int i = 0; i < Mathf.Min(2, _latency.Count); i++)
                {
                    LatencyByFinger l = _latency[i];
                    GUILayout.Label($"{HandRu(l.Hand)} палець {l.Finger}:  {l.AvgDeltaMs:+0.0;-0.0;0} мс", _sub);
                }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Детальніше", GUILayout.Height(38f)))
                _onShowStats?.Invoke();

            GUILayout.EndArea();
        }

        private void DrawImprovement(double? improvement)
        {
            string text;
            Color color;
            if (!improvement.HasValue) { text = "— перший прохід"; color = Color.gray; }
            else if (improvement.Value > 0) { text = $"▲ +{improvement.Value:0.0}"; color = new Color(0.4f, 1f, 0.4f); }
            else if (improvement.Value < 0) { text = $"▼ {improvement.Value:0.0}"; color = new Color(1f, 0.45f, 0.45f); }
            else { text = "= без змін"; color = Color.white; }

            Color prev = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(text, _sub);
            GUI.contentColor = prev;
        }

        private void DrawTrackList(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 10f, r.width - 20f, r.height - 20f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_tracks.Count == 0)
                GUILayout.Label("Під фільтр нічого не підійшло — змініть діапазони.", _sub);
            foreach (MidiTrack t in _tracks)
                DrawTrackRow(t);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTrackRow(MidiTrack t)
        {
            Rect outer = GUILayoutUtility.GetRect(1f, 66f, GUILayout.ExpandWidth(true));
            var row = new Rect(outer.x, outer.y + 3f, outer.width, outer.height - 6f);
            bool sel = t.TrackId == _selectedTrackId;

            Fill(row, sel ? RowSel : RowBg);
            Fill(new Rect(row.x, row.y, 4f, row.height), sel ? Accent : AccentDim);

            // Клик по строке выбирает трек; играет верхняя кнопка «► Играть» (без дублирующей кнопки в строке).
            if (GUI.Button(new Rect(row.x, row.y, row.width, row.height), GUIContent.none, GUIStyle.none)
                && _selectedTrackId != t.TrackId)
            {
                _selectedTrackId = t.TrackId;
                LoadStats();
            }

            string composer = string.IsNullOrEmpty(t.Composer) ? "" : $" — {t.Composer}";
            GUI.Label(new Rect(row.x + 16f, row.y + 8f, row.width - 170f, 28f), $"{t.Title}{composer}", _row);
            GUI.Label(new Rect(row.x + 16f, row.y + 36f, row.width - 170f, 22f),
                $"BPM {t.Bpm:0}  ·  {t.Tonality}  ·  {t.NoteDensity:0.0} нот/с", _sub);
            GUI.Label(new Rect(row.xMax - 150f, row.y, 134f, row.height), Stars(t.Difficulty), _starsR);
        }

        private void DrawFilterBar(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 16f, r.y + 8f, r.width - 32f, r.height - 16f));

            bool changed = false;
            GUILayout.BeginHorizontal();
            _minBpm = IntStepper($"BPM від {_minBpm}", _minBpm, BpmStep, BpmLo, _maxBpm, ref changed);
            _maxBpm = IntStepper($"BPM до {_maxBpm}", _maxBpm, BpmStep, _minBpm, BpmHi, ref changed);
            _minDiff = FloatStepper($"Складн. від {_minDiff:0}", _minDiff, DiffStep, DiffLo, _maxDiff, ref changed);
            _maxDiff = FloatStepper($"Складн. до {_maxDiff:0}", _maxDiff, DiffStep, _minDiff, DiffHi, ref changed);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✋ Аплікатура", GUILayout.Width(150f)))
                _onShowFingerEditor?.Invoke();
            if (GUILayout.Button("⚙ Налаштування", GUILayout.Width(130f)))
                _onShowSettings?.Invoke();
            if (GUILayout.Button("Скинути фільтр", GUILayout.Width(150f)))
            {
                _minBpm = BpmLo; _maxBpm = BpmHi; _minDiff = DiffLo; _maxDiff = DiffHi;
                changed = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Знайдено: {_tracks.Count}", _label, GUILayout.Width(110f));
            GUILayout.Label("Сортування:", _label, GUILayout.Width(90f));
            if (SortButton("Назва", TrackSortKey.Title)) changed = true;
            if (SortButton("BPM", TrackSortKey.Bpm)) changed = true;
            if (SortButton("Складність", TrackSortKey.Difficulty)) changed = true;
            if (GUILayout.Button(_ascending ? "↑ зрост." : "↓ спад.", GUILayout.Width(86f))) { _ascending = !_ascending; changed = true; }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (changed) Refresh();
            GUILayout.EndArea();
        }

        private bool SortButton(string caption, TrackSortKey key)
        {
            bool active = _sortKey == key;
            Color prev = GUI.color;
            if (active) GUI.color = Accent;
            bool clicked = GUILayout.Button(caption, GUILayout.Width(108f));
            GUI.color = prev;
            if (clicked && !active) { _sortKey = key; return true; }
            return false;
        }

        private int IntStepper(string caption, int value, int step, int lo, int hi, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(caption, _label, GUILayout.Width(108f));
            if (GUILayout.Button("−", GUILayout.Width(30f)))
            {
                int v = Mathf.Clamp(value - step, lo, hi);
                if (v != value) { value = v; changed = true; }
            }
            if (GUILayout.Button("+", GUILayout.Width(30f)))
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
            GUILayout.Label(caption, _label, GUILayout.Width(108f));
            if (GUILayout.Button("−", GUILayout.Width(30f)))
            {
                float v = Mathf.Clamp(value - step, lo, hi);
                if (!Mathf.Approximately(v, value)) { value = v; changed = true; }
            }
            if (GUILayout.Button("+", GUILayout.Width(30f)))
            {
                float v = Mathf.Clamp(value + step, lo, hi);
                if (!Mathf.Approximately(v, value)) { value = v; changed = true; }
            }
            GUILayout.EndHorizontal();
            return value;
        }

        private static string Stars(double difficulty)
        {
            int filled = Mathf.Clamp(Mathf.CeilToInt((float)difficulty / 2f), 1, 5);
            return new string('★', filled) + new string('☆', 5 - filled);
        }

        private static string HandRu(string hand)
            => hand == "left" ? "ліва" : hand == "right" ? "права" : "—";

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>Тёмная полупрозрачная панель с тонкой синей акцент-полосой слева.</summary>
        private static void Panel(Rect r)
        {
            Fill(r, PanelBg);
            Fill(new Rect(r.x, r.y, 4f, r.height), Accent);
        }
    }
}
