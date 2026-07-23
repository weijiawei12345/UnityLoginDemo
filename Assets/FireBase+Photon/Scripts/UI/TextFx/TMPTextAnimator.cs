using TMPro;
using UnityEngine;

namespace GameUI.TextFx
{
    /// <summary>
    /// 挂到任意 TMP_Text 上即可播放可移植文字动效。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPTextAnimator : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private TMPTextEffectPreset _defaultPreset;
        [SerializeField] private bool _playOnEnable;

        private TMPTextEffectPlayer _player;

        public TMP_Text Text
        {
            get
            {
                if (_text == null)
                {
                    _text = GetComponent<TMP_Text>();
                }

                return _text;
            }
        }

        public bool IsPlaying => _player != null && _player.IsPlaying;

        private void Awake()
        {
            EnsurePlayer();
        }

        private void OnEnable()
        {
            if (_playOnEnable && _defaultPreset != null)
            {
                PlayLoop(_defaultPreset);
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }

        public void Play(TMPTextEffectPreset preset)
        {
            EnsurePlayer();
            _player.Play(preset, loop: false, manualUpdate: false);
        }

        public void PlayLoop(TMPTextEffectPreset preset)
        {
            EnsurePlayer();
            _player.Play(preset, loop: true, manualUpdate: false);
        }

        /// <summary>
        /// 编辑器预览用：非 Play Mode 下走 DOTween ManualUpdate。
        /// </summary>
        public void PlayPreview(TMPTextEffectPreset preset, bool loop)
        {
            EnsurePlayer();
            bool manual = !Application.isPlaying;
            _player.Play(preset, loop, manualUpdate: manual);
        }

        public TMPTextEffectPreset GetDefaultPresetCopy()
        {
            return _defaultPreset != null ? _defaultPreset.Clone() : null;
        }

        public void SetDefaultPreset(TMPTextEffectPreset preset)
        {
            _defaultPreset = preset != null ? preset.Clone() : null;
        }

        public void Play(TMPTextEffectType type, string text = null)
        {
            Play(CreatePreset(type, text));
        }

        public void PlayLoop(TMPTextEffectType type, string text = null)
        {
            PlayLoop(CreatePreset(type, text));
        }

        public void Stop()
        {
            _player?.Stop(restoreMesh: true);
        }

        private void EnsurePlayer()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }

            if (_player == null)
            {
                _player = new TMPTextEffectPlayer(_text);
            }
        }

        private static TMPTextEffectPreset CreatePreset(TMPTextEffectType type, string text)
        {
            switch (type)
            {
                case TMPTextEffectType.TypewriterFade:
                    return TMPTextEffectPreset.DefaultTypewriter(text ?? string.Empty);
                case TMPTextEffectType.Wave:
                    return TMPTextEffectPreset.DefaultWave(text ?? string.Empty);
                case TMPTextEffectType.LoadingDots:
                    return TMPTextEffectPreset.DefaultLoadingDots(string.IsNullOrEmpty(text) ? "Loading" : text);
                default:
                    return TMPTextEffectPreset.DefaultBounceJump(string.IsNullOrEmpty(text) ? "Loading" : text);
            }
        }
    }
}
