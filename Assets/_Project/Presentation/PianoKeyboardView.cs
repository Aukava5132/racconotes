using System.Collections.Generic;
using UnityEngine;
using Racconotes.Domain.Enums;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Рисует фортепианную клавиатуру (белые + чёрные клавиши) и линию попадания по
    /// <see cref="PianoLayout"/>. Клавиши кратко подсвечиваются при нажатии и при оценке.
    /// Всё — общими белыми спрайтами, без ассетов.
    /// </summary>
    public sealed class PianoKeyboardView : MonoBehaviour
    {
        private const float WhiteHeight = 3.0f;
        private const float BlackHeight = 1.9f;
        private const float FlashTime = 0.18f;

        private PianoLayout _layout;
        private float _hitLineY;

        private readonly Dictionary<int, SpriteRenderer> _keys = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, Color> _baseColors = new Dictionary<int, Color>();
        private readonly Dictionary<int, float> _flash = new Dictionary<int, float>();
        private readonly Dictionary<int, Color> _flashColor = new Dictionary<int, Color>();

        public void Build(PianoLayout layout, float hitLineY)
        {
            _layout = layout;
            _hitLineY = hitLineY;

            CreateQuad("HitLine", new Vector3(0f, hitLineY, 0f),
                new Vector3(layout.TotalWidth, 0.1f, 1f), new Color(1f, 1f, 1f, 0.85f), 5);

            for (int midi = layout.LowMidi; midi <= layout.HighMidi; midi++)
                if (!PianoLayout.IsBlack(midi)) CreateKey(midi, white: true);
            for (int midi = layout.LowMidi; midi <= layout.HighMidi; midi++)
                if (PianoLayout.IsBlack(midi)) CreateKey(midi, white: false);
        }

        private void CreateKey(int midi, bool white)
        {
            float w = _layout.WidthForMidi(midi) * (white ? 0.97f : 1f);
            float h = white ? WhiteHeight : BlackHeight;
            float x = _layout.XForMidi(midi);
            float y = _hitLineY - h / 2f; // клавиши свисают вниз от линии попадания
            Color baseColor = white ? new Color(0.95f, 0.95f, 0.97f) : new Color(0.10f, 0.10f, 0.13f);

            SpriteRenderer sr = CreateQuad($"Key{midi}", new Vector3(x, y, 0f),
                new Vector3(w, h, 1f), baseColor, white ? 0 : 1);
            _keys[midi] = sr;
            _baseColors[midi] = baseColor;
        }

        private SpriteRenderer CreateQuad(string name, Vector3 pos, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprites.White();
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Подсветить клавишу цветом оценки (приоритетнее подсветки нажатия).</summary>
        public void Flash(int midi, Judgement judgement)
        {
            if (!_keys.ContainsKey(midi)) return;
            _flash[midi] = FlashTime;
            _flashColor[midi] = JudgementVisuals.Tint(judgement);
        }

        /// <summary>Кратко подсветить клавишу на нажатие (если нет активной подсветки оценки).</summary>
        public void FlashPress(int midi)
        {
            if (!_keys.ContainsKey(midi)) return;
            if (_flash.TryGetValue(midi, out float t) && t > 0f) return;
            _flash[midi] = 0.1f;
            _flashColor[midi] = new Color(0.55f, 0.8f, 1f);
        }

        private void Update()
        {
            if (_flash.Count == 0) return;

            var midis = new List<int>(_flash.Keys);
            foreach (int midi in midis)
            {
                float t = _flash[midi] - Time.deltaTime;
                if (t <= 0f)
                {
                    _flash.Remove(midi);
                    _keys[midi].color = _baseColors[midi];
                }
                else
                {
                    _flash[midi] = t;
                    _keys[midi].color = Color.Lerp(_baseColors[midi], _flashColor[midi], Mathf.Clamp01(t / FlashTime));
                }
            }
        }
    }
}
