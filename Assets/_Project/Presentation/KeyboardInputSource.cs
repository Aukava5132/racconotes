using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Racconotes.Application.Engine;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Источник ввода: читает физическую клавиатуру через Input System (<c>Keyboard.current</c>) и
    /// превращает нажатия назначенных клавиш в <see cref="InputEvent"/> со временем песни в мс.
    /// Дефолтный FPS-шаблон InputSystem_Actions не используется. Высоту даёт <see cref="PianoKeyMap"/>
    /// от базовой ноты отображаемого диапазона.
    /// </summary>
    public sealed class KeyboardInputSource : MonoBehaviour
    {
        private int _baseMidi;
        private SongClock _clock;
        private List<Key> _keys;

        public void Init(int baseMidi, SongClock clock)
        {
            _baseMidi = baseMidi;
            _clock = clock;
            _keys = new List<Key>(PianoKeyMap.MappedKeys);
        }

        /// <summary>Дописать в буфер нажатия этого кадра как <see cref="InputEvent"/>.</summary>
        public void CollectPresses(List<InputEvent> buffer)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || _keys == null) return;

            foreach (Key key in _keys)
            {
                if (!kb[key].wasPressedThisFrame) continue;

                int? midi = PianoKeyMap.MidiFor(key, _baseMidi);
                if (midi.HasValue)
                    buffer.Add(new InputEvent(_clock.Milliseconds, midi.Value, 0));
            }
        }
    }
}
