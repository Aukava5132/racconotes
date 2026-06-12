using UnityEngine;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Накладывает текстовые подписи на клавиши и летящие ноты через IMGUI (как и остальной текст
    /// в проекте — без TMP). Позиции спрайтов (мировые) проецируются в экранные координаты
    /// <see cref="Camera.WorldToScreenPoint"/>. Режимы берутся из настроек (см. <see cref="LabelMode"/>);
    /// цвет текста — чёрный на белой клавише/ноте, белый на чёрной (<see cref="NoteLabels.LabelColor"/>),
    /// с контрастной тенью для читаемости поверх перекрашенных при судействе нот.
    /// </summary>
    public sealed class LabelOverlay : MonoBehaviour
    {
        // Высоты клавиш — те же, что в PianoKeyboardView (клавиши свисают вниз от линии попадания).
        private const float WhiteHeight = 3.0f;
        private const float BlackHeight = 1.9f;

        private PianoLayout _layout;
        private int _baseMidi;
        private float _hitLineY;
        private NoteSpawner _spawner;
        private LabelMode _keyMode;
        private LabelMode _noteMode;

        private Camera _cam;
        private GUIStyle _style;

        public void Init(PianoLayout layout, int baseMidi, float hitLineY, NoteSpawner spawner,
            LabelMode keyMode, LabelMode noteMode)
        {
            _layout = layout;
            _baseMidi = baseMidi;
            _hitLineY = hitLineY;
            _spawner = spawner;
            _keyMode = keyMode;
            _noteMode = noteMode;
        }

        private void OnGUI()
        {
            if (_layout == null) return;
            if (_keyMode == LabelMode.Off && _noteMode == LabelMode.Off) return; // нечего рисовать

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };

            if (_keyMode != LabelMode.Off) DrawKeyLabels();
            if (_noteMode != LabelMode.Off) DrawNoteLabels();
        }

        private void DrawKeyLabels()
        {
            for (int midi = _layout.LowMidi; midi <= _layout.HighMidi; midi++)
            {
                string text = NoteLabels.Format(midi, _baseMidi, _keyMode);
                if (string.IsNullOrEmpty(text)) continue; // вне раскладки в режиме «Кнопки»

                // Белую клавишу подписываем в её нижней (только-белой) зоне, чёрную — по центру.
                float y = PianoLayout.IsBlack(midi) ? _hitLineY - BlackHeight * 0.5f : _hitLineY - WhiteHeight * 0.8f;
                DrawAt(new Vector3(_layout.XForMidi(midi), y, 0f), text, NoteLabels.LabelColor(midi));
            }
        }

        private void DrawNoteLabels()
        {
            foreach (NoteView view in _spawner.LiveViews)
            {
                int midi = view.Model.MidiNumber;
                string text = NoteLabels.Format(midi, _baseMidi, _noteMode);
                if (string.IsNullOrEmpty(text)) continue;
                DrawAt(view.transform.position, text, NoteLabels.LabelColor(midi));
            }
        }

        private void DrawAt(Vector3 world, string text, Color color)
        {
            Vector3 p = _cam.WorldToScreenPoint(world);
            if (p.z <= 0f) return; // за камерой

            var rect = new Rect(p.x - 24f, Screen.height - p.y - 12f, 48f, 24f);

            // Тень контрастного цвета — читаемость поверх перекрашенных (judged) нот.
            Color shadow = (color.r + color.g + color.b > 1.5f)
                ? new Color(0f, 0f, 0f, 0.85f)
                : new Color(1f, 1f, 1f, 0.85f);

            _style.normal.textColor = shadow;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _style);
            _style.normal.textColor = color;
            GUI.Label(rect, text, _style);
        }
    }
}
