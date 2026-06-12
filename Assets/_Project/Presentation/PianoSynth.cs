using System.Collections.Generic;
using UnityEngine;

namespace Racconotes.Presentation
{
    /// <summary>
    /// Полифоническая озвучка нот процедурным синтезом (без ассетов): предгенерирует короткий
    /// <see cref="AudioClip"/> на каждый MIDI диапазона клавиатуры (<see cref="ToneSynth"/>) и
    /// проигрывает их через один <see cref="AudioSource"/> (<c>PlayOneShot</c> — полифония).
    /// Создаётся <see cref="GameplayController"/> на время сессии. Гарантирует <see cref="AudioListener"/>
    /// в сцене (камеру код-драйв сцены создаёт без него).
    /// </summary>
    public sealed class PianoSynth : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float NoteSeconds = 0.7f;

        private AudioSource _source;
        private readonly Dictionary<int, AudioClip> _clips = new Dictionary<int, AudioClip>();

        /// <summary>Подготовить клипы для всех MIDI в диапазоне [lowMidi..highMidi] включительно.</summary>
        public void Init(int lowMidi, int highMidi)
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // 2D — без позиционирования

            EnsureListener();

            for (int midi = lowMidi; midi <= highMidi; midi++)
            {
                float[] data = ToneSynth.Render(midi, SampleRate, NoteSeconds);
                var clip = AudioClip.Create($"tone_{midi}", data.Length, 1, SampleRate, false);
                clip.SetData(data, 0);
                _clips[midi] = clip;
            }
        }

        /// <summary>Проиграть ноту по MIDI-номеру (ноты вне подготовленного диапазона тихо игнорируются).</summary>
        public void Play(int midi)
        {
            if (_source != null && _clips.TryGetValue(midi, out AudioClip clip))
                _source.PlayOneShot(clip);
        }

        /// <summary>Мастер-громкость 0..1 (PlayOneShot домножается на AudioSource.volume).</summary>
        public void SetVolume(float volume)
        {
            if (_source != null) _source.volume = Mathf.Clamp01(volume);
        }

        private static void EnsureListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>() != null) return;

            Camera cam = Camera.main;
            if (cam != null) cam.gameObject.AddComponent<AudioListener>();
        }
    }
}
