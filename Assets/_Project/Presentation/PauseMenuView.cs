using System;
using UnityEngine;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Оверлей паузы (IMGUI, мышь, без TMP — по образцу <see cref="ResultScreenView"/>). Показывается
    /// по ESC во время игры: останавливает часы (это делает <see cref="GameplayController"/>) и даёт
    /// продолжить, выйти в меню и отрегулировать громкость на лету. Сам ввод/время не трогает —
    /// только сообщает наружу через события.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        private float _volume = 1f;
        private GUIStyle _title, _row, _hint;

        public bool Visible { get; private set; }

        /// <summary>«Продолжить» — снять паузу.</summary>
        public event Action OnResume;

        /// <summary>«В главное меню» — выйти из игры без сохранения.</summary>
        public event Action OnExitToMenu;

        /// <summary>Громкость изменена слайдером (0..1) — применить и сохранить.</summary>
        public event Action<float> OnVolumeChanged;

        /// <summary>Задать стартовое положение слайдера громкости (текущая громкость сессии).</summary>
        public void Init(float currentVolume) => _volume = Mathf.Clamp01(currentVolume);

        public void Show() => Visible = true;
        public void Hide() => Visible = false;

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        }

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            const float w = 460f, h = 340f;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 28f, box.y + 22f, w - 56f, h - 44f));
            GUILayout.Label("Пауза", _title);
            GUILayout.Space(18f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Громкость:", _row, GUILayout.Width(130f));
            float v = GUILayout.HorizontalSlider(_volume, 0f, 1f, GUILayout.Height(24f));
            GUILayout.Space(10f);
            GUILayout.Label($"{Mathf.RoundToInt(_volume * 100f)}%", _row, GUILayout.Width(55f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(v - _volume) > 0.001f)
            {
                _volume = v;
                OnVolumeChanged?.Invoke(v);
            }

            GUILayout.Space(22f);
            if (GUILayout.Button("Продолжить", GUILayout.Height(44f)))
                OnResume?.Invoke();
            GUILayout.Space(8f);
            if (GUILayout.Button("В главное меню", GUILayout.Height(44f)))
                OnExitToMenu?.Invoke();

            GUILayout.FlexibleSpace();
            GUILayout.Label("ESC — продолжить", _hint);
            GUILayout.EndArea();
        }
    }
}
