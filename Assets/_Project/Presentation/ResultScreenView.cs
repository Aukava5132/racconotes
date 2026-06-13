using UnityEngine;
using Racconotes.Domain.Entities;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Экран результатов из <see cref="GameResult"/> (точность, разбивка оценок, макс. комбо).
    /// Рендер через IMGUI (только подписи, без кнопок-событий — перезапуск ловит контроллер по R).
    /// </summary>
    public sealed class ResultScreenView : MonoBehaviour
    {
        private GameResult _result;
        private GUIStyle _title, _row, _hint;

        public bool Visible { get; private set; }

        public void ShowResult(GameResult result)
        {
            _result = result;
            Visible = true;
        }

        public void Hide() => Visible = false;

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        }

        private void OnGUI()
        {
            if (!Visible || _result == null) return;
            EnsureStyles();

            const float w = 520f, h = 380f;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 28f, box.y + 22f, w - 56f, h - 44f));
            GUILayout.Label("Результат", _title);
            GUILayout.Space(14f);
            GUILayout.Label($"Точність:        {_result.AccuracyPercent:0.0}%", _row);
            GUILayout.Label($"Perfect (300):   {_result.Score300}", _row);
            GUILayout.Label($"Good (100):      {_result.Score100}", _row);
            GUILayout.Label($"Bad (50):        {_result.Score50}", _row);
            GUILayout.Label($"Miss:            {_result.MissCount}", _row);
            GUILayout.Label($"Макс. комбо:     {_result.MaxCombo}", _row);
            GUILayout.FlexibleSpace();
            GUILayout.Label("R — грати знову   ·   M — у меню", _hint);
            GUILayout.EndArea();
        }
    }
}
