using UnityEngine;
using Racconotes.Domain.Enums;

namespace Racconotes.Presentation
{
    /// <summary>
    /// HUD: комбо, текущая точность и всплывающая оценка последнего нажатия. Рендер через IMGUI
    /// (OnGUI) — без ассетов шрифта и без зависимости от TMP Essentials, рисуется каждый кадр.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private int _combo;
        private double _accuracy = 100.0;
        private Judgement _last;
        private bool _hasJudgement;
        private float _judgeFade;

        private GUIStyle _comboStyle, _accStyle, _judgeStyle;

        public void Show(int combo, double accuracy, Judgement last)
        {
            _combo = combo;
            _accuracy = accuracy;
            _last = last;
            _hasJudgement = true;
            _judgeFade = 0.6f;
        }

        public void SetAccuracy(double accuracy) => _accuracy = accuracy;

        private void Update()
        {
            if (_judgeFade > 0f) _judgeFade -= Time.deltaTime;
        }

        private void EnsureStyles()
        {
            if (_comboStyle != null) return;
            _comboStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
            _accStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.UpperRight };
            _judgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 50, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 18f, 420f, 60f), _combo > 1 ? $"{_combo}x" : string.Empty, _comboStyle);
            GUI.Label(new Rect(Screen.width - 340f, 18f, 316f, 50f), $"{_accuracy:0.0}%", _accStyle);

            if (_hasJudgement && _judgeFade > 0f)
            {
                Color c = JudgementVisuals.Tint(_last);
                c.a = Mathf.Clamp01(_judgeFade / 0.6f);
                GUI.color = c;
                GUI.Label(new Rect(0f, Screen.height * 0.28f, Screen.width, 72f), JudgementVisuals.Label(_last), _judgeStyle);
                GUI.color = Color.white;
            }
        }
    }
}
