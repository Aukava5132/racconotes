using UnityEngine;
using Racconotes.Domain.Entities;
using Racconotes.Domain.Enums;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Вью одной падающей ноты. Позиция по Y от времени песни: при songTime == StartTime нота
    /// на линии попадания. X и ширина — из <see cref="PianoLayout"/>; цвет — белая/чёрная клавиша,
    /// перекрашивается при судействе. Спрайт — общий белый 1×1 (<see cref="PrimitiveSprites"/>).
    /// </summary>
    public sealed class NoteView : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private PianoLayout _layout;
        private float _fallSpeed;
        private float _hitLineY;
        private float _halfHeight;

        public Note Model { get; private set; }
        public bool Consumed { get; private set; }
        public bool Holding { get; private set; }

        public void Init(Note model, PianoLayout layout, float fallSpeed, float hitLineY)
        {
            Model = model;
            _layout = layout;
            _fallSpeed = fallSpeed;
            _hitLineY = hitLineY;

            _sr = gameObject.GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = PrimitiveSprites.White();
            _sr.sortingOrder = PianoLayout.IsBlack(model.MidiNumber) ? 3 : 2;

            float w = layout.WidthForMidi(model.MidiNumber) * 0.9f;
            float h = Mathf.Max(0.45f, (float)model.Duration * fallSpeed);
            _halfHeight = h / 2f;
            transform.localScale = new Vector3(w, h, 1f);
            _sr.color = BaseColor();
        }

        private Color BaseColor() => PianoLayout.IsBlack(Model.MidiNumber)
            ? new Color(0.25f, 0.45f, 0.85f)
            : new Color(0.40f, 0.72f, 1.00f);

        /// <summary>
        /// Поставить ноту в позицию для текущего времени песни (в секундах). Спрайт смещён вверх
        /// на половину высоты, чтобы линии попадания достигал нижний край (голова) ровно в момент
        /// <see cref="Note.StartTime"/> — когда нота и судится. Хвост (верх) приходит к линии в
        /// StartTime + Duration (основа удержания).
        /// </summary>
        public void SetSongTime(double songTime)
        {
            float x = _layout.XForMidi(Model.MidiNumber);
            float y = _hitLineY + (float)(Model.StartTime - songTime) * _fallSpeed + _halfHeight;
            transform.position = new Vector3(x, y, 0f);
        }

        /// <summary>Отметить длинную ноту удерживаемой (голова нажата) — пока не финальная оценка.</summary>
        public void MarkHolding()
        {
            Holding = true;
            _sr.color = new Color(1f, 0.85f, 0.30f); // янтарный — «зажато»
        }

        /// <summary>Отметить ноту оценённой (попадание или визуальный промах) и перекрасить.</summary>
        public void MarkJudged(Judgement judgement)
        {
            Consumed = true;
            Holding = false;
            _sr.color = JudgementVisuals.Tint(judgement);
        }

        public void Despawn() => Destroy(gameObject);
    }
}
