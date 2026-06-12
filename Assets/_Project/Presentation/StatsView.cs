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
    /// Экран статистики (§1.4 «отображение статистики»; §2.4). Вызывает все 4 сложных
    /// аналитических запроса через <see cref="GameContext.StatsQueries"/> (реальный SQL —
    /// демонстрация критерия «БД как активный компонент») и показывает их результат:
    /// 1) прогресс точности по треку (LAG/OVER), 2) самые сложные ноты (top-10 промахов),
    /// 3) рекомендации треков, 4) средняя задержка по руке/пальцу.
    /// Рендер через IMGUI (мышь, без TMP) по образцу <see cref="TrackSelectView"/>.
    /// Трек для графика прогресса (Запрос 1 требует trackId) выбирается переключателем ◄/►.
    /// </summary>
    public sealed class StatsView : MonoBehaviour
    {
        private GameContext _ctx;
        private int _userId;

        private List<MidiTrack> _tracks = new List<MidiTrack>();
        private int _progressIndex;

        private IReadOnlyList<AccuracyPoint> _progress = Array.Empty<AccuracyPoint>();
        private IReadOnlyList<WeakSpot> _weak = Array.Empty<WeakSpot>();
        private IReadOnlyList<TrackRecommendation> _recommend = Array.Empty<TrackRecommendation>();
        private IReadOnlyList<LatencyByFinger> _latency = Array.Empty<LatencyByFinger>();

        private Vector2 _scroll;
        private GUIStyle _title, _section, _row, _sub, _hint;

        public bool Visible { get; private set; }

        /// <summary>Запрос вернуться в меню (кнопка «Назад»).</summary>
        public event Action OnBack;

        public void Init(GameContext ctx, int userId)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _userId = userId;
        }

        public void Show() { Visible = true; LoadAll(); }
        public void Hide() => Visible = false;

        /// <summary>Перечитать всю статистику из БД (свежие данные при каждом открытии экрана).</summary>
        private void LoadAll()
        {
            if (_ctx == null) return;
            _tracks = _ctx.TrackRepository.GetAllTracks().ToList();
            _weak = _ctx.StatsQueries.GetWeakSpots(_userId, 10);
            _recommend = _ctx.StatsQueries.RecommendTracks(_userId);
            _latency = _ctx.StatsQueries.GetAverageLatency(_userId);
            _progressIndex = PickDefaultProgressIndex();
            LoadProgress();
        }

        /// <summary>Дефолт — первый трек, по которому уже есть проходы; иначе первый в списке.</summary>
        private int PickDefaultProgressIndex()
        {
            for (int i = 0; i < _tracks.Count; i++)
                if (_ctx.StatsQueries.GetAccuracyProgress(_userId, _tracks[i].TrackId).Count > 0)
                    return i;
            return 0;
        }

        private void LoadProgress()
        {
            if (_tracks.Count == 0) { _progress = Array.Empty<AccuracyPoint>(); return; }
            _progressIndex = ((_progressIndex % _tracks.Count) + _tracks.Count) % _tracks.Count;
            _progress = _ctx.StatsQueries.GetAccuracyProgress(_userId, _tracks[_progressIndex].TrackId);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _section = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            float w = Mathf.Min(760f, Screen.width - 40f);
            float h = Mathf.Min(680f, Screen.height - 40f);
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 24f, box.y + 18f, w - 48f, h - 36f));

            GUILayout.Label("Статистика", _title);
            GUILayout.Space(8f);

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawProgressSection();
            DrawWeakSpotsSection();
            DrawRecommendSection();
            DrawLatencySection();
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("Назад", GUILayout.Height(40f)))
                OnBack?.Invoke();

            GUILayout.EndArea();
        }

        private void DrawProgressSection()
        {
            GUILayout.Label("1. Прогресс точности по треку (LAG/OVER)", _section);
            if (_tracks.Count == 0)
            {
                GUILayout.Label("В библиотеке нет треков.", _sub);
                GUILayout.Space(12f);
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◄", GUILayout.Width(40f))) { _progressIndex--; LoadProgress(); }
            GUILayout.Label(_tracks[_progressIndex].Title, _row, GUILayout.Width(420f));
            if (GUILayout.Button("►", GUILayout.Width(40f))) { _progressIndex++; LoadProgress(); }
            GUILayout.EndHorizontal();

            if (_progress.Count == 0)
            {
                GUILayout.Label("Нет проходов. Сыграйте трек несколько раз, чтобы увидеть динамику.", _sub);
            }
            else
            {
                foreach (AccuracyPoint p in _progress)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(p.PlayedAt.ToString("yyyy-MM-dd HH:mm"), _sub, GUILayout.Width(190f));
                    GUILayout.Label($"{p.Accuracy:0.0}%", _sub, GUILayout.Width(80f));
                    DrawImprovement(p.Improvement);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Space(12f);
        }

        private void DrawImprovement(double? improvement)
        {
            string text;
            Color color;
            if (!improvement.HasValue) { text = "— (первый проход)"; color = Color.gray; }
            else if (improvement.Value > 0) { text = $"▲ +{improvement.Value:0.0}"; color = new Color(0.4f, 1f, 0.4f); }
            else if (improvement.Value < 0) { text = $"▼ {improvement.Value:0.0}"; color = new Color(1f, 0.45f, 0.45f); }
            else { text = "= без изменений"; color = Color.white; }

            Color prev = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(text, _sub);
            GUI.contentColor = prev;
        }

        private void DrawWeakSpotsSection()
        {
            GUILayout.Label("2. Самые сложные ноты — где чаще промах (top-10)", _section);
            if (_weak.Count == 0)
                GUILayout.Label("Промахов пока нет — сыграйте несколько треков.", _sub);
            else
                foreach (WeakSpot s in _weak)
                    GUILayout.Label(
                        $"{NoteNaming.Name(s.MidiNumber)}   ·   {HandRu(s.Hand)}, палец {s.Finger}   ·   промахов: {s.MissCount}",
                        _sub);
            GUILayout.Space(12f);
        }

        private void DrawRecommendSection()
        {
            GUILayout.Label("3. Рекомендованные треки (давно не играли + по уровню)", _section);
            if (_recommend.Count == 0)
                GUILayout.Label("Нет рекомендаций под ваш уровень.", _sub);
            else
                foreach (TrackRecommendation r in _recommend)
                    GUILayout.Label($"{r.Title}   ·   сложность {r.Difficulty:0.0}   ·   BPM {r.Bpm:0}", _sub);
            GUILayout.Space(12f);
        }

        private void DrawLatencySection()
        {
            GUILayout.Label("4. Средняя задержка по руке/пальцу (AVG delta_ms)", _section);
            if (_latency.Count == 0)
                GUILayout.Label("Недостаточно данных о попаданиях.", _sub);
            else
                foreach (LatencyByFinger l in _latency)
                {
                    string dir = l.AvgDeltaMs > 0 ? "опаздывает" : l.AvgDeltaMs < 0 ? "торопится" : "ровно";
                    GUILayout.Label(
                        $"{HandRu(l.Hand)}, палец {l.Finger}   ·   {l.AvgDeltaMs:+0.0;-0.0;0} мс ({dir})   ·   нажатий: {l.HitCount}",
                        _sub);
                }
            GUILayout.Space(4f);
        }

        private static string HandRu(string hand)
        {
            if (hand == "left") return "левая";
            if (hand == "right") return "правая";
            return "—";
        }
    }
}
