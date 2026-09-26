using UnityEngine;

namespace MySys22.DialogueEngine.Core
{
    [DisallowMultipleComponent]
    public sealed class DialogueAudioPlayer : MonoBehaviour
    {
        private AudioSource _ambient;
        private AudioSource _voice;

        public string CurrentTrack { get; private set; }

        public bool IsPlaying => _ambient != null && _ambient.isPlaying;

        public AudioSource AmbientSource => _ambient;

        public AudioSource VoiceSource => _voice;

        private void Awake()
        {
            _ambient = CreateSource("Ambient", loop: true, volume: 0.6f);
            _voice = CreateSource("Voice", loop: false, volume: 1f);
        }

        private AudioSource CreateSource(string name, bool loop, float volume)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            return source;
        }

        public void PlayAmbient(AudioClip clip, bool loop, string track)
        {
            if (_ambient == null) return;
            CurrentTrack = track;
            _ambient.clip = clip;
            _ambient.loop = loop;
            _ambient.Play();
        }

        public void PlayVoice(AudioClip clip)
        {
            if (_voice == null || clip == null) return;
            _voice.clip = clip;
            _voice.Play();
        }

        public void StopAmbient()
        {
            CurrentTrack = null;
            if (_ambient == null) return;
            _ambient.Stop();
            _ambient.clip = null;
        }

        public void StopVoice()
        {
            if (_voice == null) return;
            _voice.Stop();
            _voice.clip = null;
        }

        public void SetVolume(float ambient, float voice)
        {
            if (_ambient != null) _ambient.volume = Mathf.Clamp01(ambient);
            if (_voice != null) _voice.volume = Mathf.Clamp01(voice);
        }
    }
}
