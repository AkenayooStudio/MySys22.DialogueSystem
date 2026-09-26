using System;
using System.Collections.Generic;
using UnityEngine;

namespace MySys22.DialogueEngine.Core
{
    public interface IDialogueAudioProvider
    {
        void Load(string track, Action<AudioClip> onLoaded);
    }

    public static class DialogueAudio
    {
        private static readonly Dictionary<string, AudioClip> Registry =
            new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        private static DialogueAudioPlayer _player;
        private static IDialogueAudioProvider _provider;
        private static bool _voiceEnabled = true;

        public static IDialogueAudioProvider Provider
        {
            get => _provider;
            set => _provider = value;
        }

        public static bool VoiceEnabled
        {
            get => _voiceEnabled;
            set => _voiceEnabled = value;
        }

        public static bool IsPlaying => _player != null && _player.IsPlaying;

        public static string CurrentTrack => _player?.CurrentTrack;

        public static AudioSource AmbientSource => Player?.AmbientSource;

        public static AudioSource VoiceSource => Player?.VoiceSource;

        private static DialogueAudioPlayer Player
        {
            get
            {
                if (_player != null) return _player;
                var host = new GameObject("MySys22.DialogueAudio");
                UnityEngine.Object.DontDestroyOnLoad(host);
                _player = host.AddComponent<DialogueAudioPlayer>();
                return _player;
            }
        }

        public static void Register(string track, AudioClip clip)
        {
            if (string.IsNullOrEmpty(track) || clip == null) return;
            Registry[track] = clip;
        }

        public static void ClearRegistry() => Registry.Clear();

        public static void Play(string track, bool loop = false)
        {
            if (string.IsNullOrEmpty(track)) return;
            Resolve(track, clip =>
            {
                if (clip == null)
                {
                    DialogueLogger.LogWarning($"Audio track not found: '{track}'.");
                    return;
                }
                Player.PlayAmbient(clip, loop, track);
            });
        }

        public static void PlayVoice(string speaker, int did)
        {
            if (!_voiceEnabled || string.IsNullOrEmpty(speaker)) return;

            string track = $"{speaker}/{did}";
            Resolve(track, clip =>
            {
                if (clip == null) return;
                Player.PlayVoice(clip);
            });
        }

        public static void Stop()
        {
            if (_player == null) return;
            _player.StopAmbient();
        }

        public static void StopVoice()
        {
            if (_player == null) return;
            _player.StopVoice();
        }

        public static void StopAll()
        {
            if (_player == null) return;
            _player.StopAmbient();
            _player.StopVoice();
        }

        private static void Resolve(string track, Action<AudioClip> onLoaded)
        {
            if (Registry.TryGetValue(track, out AudioClip registered))
            {
                onLoaded(registered);
                return;
            }

            if (_provider != null)
            {
                _provider.Load(track, onLoaded);
                return;
            }

            var loaded = Resources.Load<AudioClip>(track);
            onLoaded(loaded);
        }
    }
}
