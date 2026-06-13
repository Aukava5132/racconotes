using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Racconotes.Application.Engine;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Источник ввода мышью: левый клик по нарисованной клавише фортепиано превращается в нажатие
    /// ноты (<see cref="InputEvent"/>), удержание кнопки мыши — в удержание длинной ноты, отпускание —
    /// в release. Контракт повторяет <see cref="KeyboardInputSource"/> (CollectPresses/CollectReleases),
    /// поэтому <see cref="GameplayController"/> кормит оба источника в общие буферы, а вся остальная
    /// цепочка (звук, подсветка, судейство, удержания) переиспользуется без изменений. Какая клавиша
    /// под курсором — чистый хит-тест <see cref="PianoLayout.KeyAt"/> по той же геометрии, что рисует
    /// <see cref="PianoKeyboardView"/>.
    /// </summary>
    public sealed class MouseInputSource : MonoBehaviour
    {
        private PianoLayout _layout;
        private float _hitLineY;
        private SongClock _clock;
        private Camera _camera;
        private int? _heldMidi; // клавиша, «зажатая» мышью (для финализации удержаний по отпусканию)

        public void Init(PianoLayout layout, float hitLineY, SongClock clock)
        {
            _layout = layout;
            _hitLineY = hitLineY;
            _clock = clock;
            _camera = Camera.main;
        }

        /// <summary>Дописать в буфер нажатие этого кадра (левый клик по клавише) как <see cref="InputEvent"/>.</summary>
        public void CollectPresses(List<InputEvent> buffer)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _layout == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            int? midi = KeyUnderCursor(mouse);
            if (!midi.HasValue) return;

            _heldMidi = midi;
            buffer.Add(new InputEvent(_clock.Milliseconds, midi.Value, 0));
        }

        /// <summary>Дописать в буфер MIDI клавиши, «отпущенной» мышью в этом кадре (для удержаний).</summary>
        public void CollectReleases(List<int> buffer)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.wasReleasedThisFrame || !_heldMidi.HasValue) return;

            buffer.Add(_heldMidi.Value);
            _heldMidi = null;
        }

        private int? KeyUnderCursor(Mouse mouse)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return null;

            Vector2 screen = mouse.position.ReadValue();
            // Ортокамера: z-параметр на (x,y) не влияет, но берём расстояние до плоскости z=0 ради корректности.
            Vector3 world = _camera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            return _layout.KeyAt(world.x, world.y, _hitLineY,
                PianoKeyboardView.WhiteHeight, PianoKeyboardView.BlackHeight);
        }
    }
}
