using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GameUI.TextFx
{
    /// <summary>
    /// 根据预设构建 DOTween Sequence，并在 OnUpdate 中写回 TMP mesh。
    /// </summary>
    public sealed class TMPTextEffectPlayer
    {
        private readonly TMP_Text _text;
        private readonly List<Tween> _tweens = new List<Tween>();
        private List<TMPTextMeshUtility.CharMeshRef> _chars;
        private Sequence _masterSequence;
        private Tweener _fadeTween;
        private bool _manualUpdate;

        public TMPTextEffectPlayer(TMP_Text text)
        {
            _text = text;
        }

        public bool IsPlaying =>
            (_masterSequence != null && _masterSequence.IsActive()) ||
            (_fadeTween != null && _fadeTween.IsActive()) ||
            _tweens.Exists(t => t != null && t.IsActive());

        public void Play(TMPTextEffectPreset preset, bool loop, bool manualUpdate = false)
        {
            Stop(restoreMesh: true);
            if (_text == null || preset == null)
            {
                return;
            }

            _manualUpdate = manualUpdate;

            if (!string.IsNullOrEmpty(preset.DisplayText))
            {
                _text.text = preset.DisplayText;
            }

            _text.alpha = 1f;

            switch (preset.EffectType)
            {
                case TMPTextEffectType.LoadingDots:
                    PlayLoadingDots(preset, loop);
                    break;
                case TMPTextEffectType.Wave:
                    PlayWave(preset, loop);
                    break;
                case TMPTextEffectType.TypewriterFade:
                    PlayTypewriterFade(preset, loop);
                    break;
                default:
                    PlayBounceJump(preset, loop);
                    break;
            }
        }

        public void Stop(bool restoreMesh)
        {
            if (_masterSequence != null && _masterSequence.IsActive())
            {
                _masterSequence.Kill();
            }

            _masterSequence = null;

            if (_fadeTween != null && _fadeTween.IsActive())
            {
                _fadeTween.Kill();
            }

            _fadeTween = null;

            for (int i = 0; i < _tweens.Count; i++)
            {
                if (_tweens[i] != null && _tweens[i].IsActive())
                {
                    _tweens[i].Kill();
                }
            }

            _tweens.Clear();

            if (restoreMesh && _chars != null && _chars.Count > 0)
            {
                TMPTextMeshUtility.RestoreOriginalMesh(_text, _chars);
            }

            _chars = null;

            if (_text != null)
            {
                DOTween.Kill(_text);
            }
        }

        private void PlayLoadingDots(TMPTextEffectPreset preset, bool loop)
        {
            string baseText = string.IsNullOrEmpty(preset.DotsBaseText) ? "Loading" : preset.DotsBaseText;
            baseText = baseText.TrimEnd('.', '…', '．');
            if (string.IsNullOrEmpty(baseText))
            {
                baseText = "Loading";
            }

            _text.text = baseText;
            _text.alpha = 1f;
            // 动态追加 "." 时禁止 Ellipsis 裁剪，否则末尾点会被 TMP 吃掉
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Overflow;

            float interval = Mathf.Max(0.05f, preset.DotsInterval);
            Sequence dots = DOTween.Sequence()
                .SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale)
                .AppendCallback(() => _text.text = baseText)
                .AppendInterval(interval)
                .AppendCallback(() => _text.text = baseText + ".")
                .AppendInterval(interval)
                .AppendCallback(() => _text.text = baseText + "..")
                .AppendInterval(interval)
                .AppendCallback(() => _text.text = baseText + "...")
                .AppendInterval(interval)
                .SetTarget(_text);

            if (loop)
            {
                dots.SetLoops(-1, LoopType.Restart);
            }

            _masterSequence = dots;

            _fadeTween = _text
                .DOFade(preset.PulseMinAlpha, preset.PulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale)
                .SetTarget(_text);
        }

        private void PlayBounceJump(TMPTextEffectPreset preset, bool loop)
        {
            _chars = TMPTextMeshUtility.CollectVisibleChars(_text, includeWhitespace: false);
            if (_chars.Count == 0)
            {
                return;
            }

            bool useFade = preset.FadeInDuration > 0f || preset.FadeOutDuration > 0f;
            var states = CreateStates(_chars, preset, startAlpha: useFade ? 0f : 1f);
            bool ignoreTimeScale = preset.IgnoreTimeScale;
            UpdateType updateType = _manualUpdate ? UpdateType.Manual : UpdateType.Normal;
            _masterSequence = DOTween.Sequence().SetUpdate(updateType, ignoreTimeScale).SetTarget(_text);

            // 循环时若开启了淡入但未配淡出，自动补同等时长淡出，否则下一轮看不到淡入。
            // FadeInDuration=0 且 FadeOutDuration=0 表示关闭逐字透明度动画，只保留跳动。
            float fadeOutDuration = ResolveLoopFadeOutDuration(preset, loop);
            float moveSpan = preset.UseAnimationCurve ? preset.Duration : preset.Duration * 2f;

            for (int i = 0; i < states.Count; i++)
            {
                CharState state = states[i];
                float at = i * preset.CharDelay;

                Vector3 endPos = new Vector3(0f, preset.JumpHeight, 0f);
                Color endColor = preset.ChangeColor ? preset.EndColor : WithAlpha(state.OriColor, 1f);
                Color startColor = useFade ? WithAlpha(endColor, 0f) : endColor;
                state.Color = startColor;
                state.Pos = Vector3.zero;

                // 直接 Insert 到 master，避免嵌套 Sequence 导致短 tween 在 Restart 时不复位
                _masterSequence.InsertCallback(at, () =>
                {
                    state.Color = startColor;
                    state.Pos = Vector3.zero;
                });

                Tweener move = DOTween.To(() => state.Pos, x => state.Pos = x, endPos, preset.Duration)
                    .SetUpdate(updateType, ignoreTimeScale);
                ApplyMoveEase(move, preset);
                if (!preset.UseAnimationCurve)
                {
                    move.SetLoops(2, LoopType.Yoyo);
                }

                _masterSequence.Insert(at, move);
                _tweens.Add(move);

                if (preset.FadeInDuration > 0f)
                {
                    Tweener fadeIn = DOTween.To(() => state.Color, x => state.Color = x, endColor, preset.FadeInDuration)
                        .From(startColor)
                        .SetUpdate(updateType, ignoreTimeScale);
                    ApplyFadeEase(fadeIn, preset);
                    _masterSequence.Insert(at, fadeIn);
                    _tweens.Add(fadeIn);
                }

                if (fadeOutDuration > 0f)
                {
                    Color fadeOutColor = WithAlpha(endColor, 0f);
                    float fadeOutAt = at + Mathf.Max(moveSpan, Mathf.Max(0f, preset.FadeInDuration));
                    Tweener fadeOut = DOTween.To(() => state.Color, x => state.Color = x, fadeOutColor, fadeOutDuration)
                        .SetUpdate(updateType, ignoreTimeScale);
                    ApplyFadeEase(fadeOut, preset);
                    _masterSequence.Insert(fadeOutAt, fadeOut);
                    _tweens.Add(fadeOut);
                }
            }

            _masterSequence.OnUpdate(() => ApplyAllCharMeshes(states, preset.ChangeColor));

            if (loop)
            {
                _masterSequence.SetLoops(-1, LoopType.Restart);
            }
        }

        private void PlayTypewriterFade(TMPTextEffectPreset preset, bool loop)
        {
            _chars = TMPTextMeshUtility.CollectVisibleChars(_text, includeWhitespace: false);
            if (_chars.Count == 0)
            {
                return;
            }

            var states = CreateStates(_chars, preset, startAlpha: 0f);
            bool ignoreTimeScale = preset.IgnoreTimeScale;
            UpdateType updateType = _manualUpdate ? UpdateType.Manual : UpdateType.Normal;
            _masterSequence = DOTween.Sequence().SetUpdate(updateType, ignoreTimeScale).SetTarget(_text);

            for (int i = 0; i < states.Count; i++)
            {
                CharState state = states[i];
                state.Color = WithAlpha(state.Color, 0f);
                TMPTextMeshUtility.SetVertexPosition(
                    _text, state.MaterialIndex, state.VertexIndex, Vector3.zero, state.OriPos, state.Color, true);
            }

            TMPTextMeshUtility.ApplyMesh(_text);

            float fadeOutDuration = ResolveLoopFadeOutDuration(preset, loop);
            float holdDuration = Mathf.Max(0f, preset.Duration);

            for (int i = 0; i < states.Count; i++)
            {
                CharState state = states[i];
                float at = i * preset.CharDelay;
                Color endColor = preset.ChangeColor ? preset.EndColor : WithAlpha(state.OriColor, 1f);
                Color startColor = WithAlpha(endColor, 0f);

                if (preset.FadeInDuration > 0f)
                {
                    _masterSequence.InsertCallback(at, () => { state.Color = startColor; });
                    Tweener fadeIn = DOTween.To(() => state.Color, x => state.Color = x, endColor, preset.FadeInDuration)
                        .From(startColor)
                        .SetUpdate(updateType, ignoreTimeScale);
                    ApplyFadeEase(fadeIn, preset);
                    _masterSequence.Insert(at, fadeIn);
                    _tweens.Add(fadeIn);
                }
                else
                {
                    state.Color = endColor;
                    _masterSequence.InsertCallback(at, () => { state.Color = endColor; });
                }

                if (fadeOutDuration > 0f)
                {
                    Color fadeOutColor = WithAlpha(endColor, 0f);
                    float fadeOutAt = at + Mathf.Max(0f, preset.FadeInDuration) + holdDuration;
                    Tweener fadeOut = DOTween.To(() => state.Color, x => state.Color = x, fadeOutColor, fadeOutDuration)
                        .SetUpdate(updateType, ignoreTimeScale);
                    ApplyFadeEase(fadeOut, preset);
                    _masterSequence.Insert(fadeOutAt, fadeOut);
                    _tweens.Add(fadeOut);
                }
            }

            _masterSequence.OnUpdate(() => ApplyAllCharMeshes(states, changeColor: true));

            if (loop)
            {
                _masterSequence.SetLoops(-1, LoopType.Restart);
            }
        }

        /// <summary>
        /// 循环播放时保证有淡出：否则淡入结束后 alpha 停在 1，下一轮看不到淡入。
        /// </summary>
        private static float ResolveLoopFadeOutDuration(TMPTextEffectPreset preset, bool loop)
        {
            if (preset.FadeOutDuration > 0f)
            {
                return preset.FadeOutDuration;
            }

            if (loop && preset.FadeInDuration > 0f)
            {
                return preset.FadeInDuration;
            }

            return 0f;
        }

        private void ApplyAllCharMeshes(List<CharState> states, bool changeColor)
        {
            for (int i = 0; i < states.Count; i++)
            {
                CharState state = states[i];
                TMPTextMeshUtility.SetVertexPosition(
                    _text,
                    state.MaterialIndex,
                    state.VertexIndex,
                    state.Pos,
                    state.OriPos,
                    state.Color,
                    changeColor);
            }

            TMPTextMeshUtility.ApplyMesh(_text);
        }

        private void PlayWave(TMPTextEffectPreset preset, bool loop)
        {
            _chars = TMPTextMeshUtility.CollectVisibleChars(_text, includeWhitespace: false);
            if (_chars.Count == 0)
            {
                return;
            }

            var states = CreateStates(_chars, preset, startAlpha: 1f);
            float phase = 0f;
            float cycle = Mathf.Max(0.2f, 1f / Mathf.Max(0.1f, preset.WaveFrequency));

            Tweener phaseTween = DOTween.To(() => phase, x => phase = x, Mathf.PI * 2f, cycle)
                .SetEase(Ease.Linear)
                .SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale)
                .SetTarget(_text);

            if (loop)
            {
                phaseTween.SetLoops(-1, LoopType.Restart);
            }

            phaseTween.OnUpdate(() =>
            {
                for (int i = 0; i < states.Count; i++)
                {
                    CharState state = states[i];
                    float offset = i * preset.CharDelay * 10f;
                    state.Pos = new Vector3(0f, Mathf.Sin(phase + offset) * preset.WaveAmplitude, 0f);
                    TMPTextMeshUtility.SetVertexPosition(
                        _text,
                        state.MaterialIndex,
                        state.VertexIndex,
                        state.Pos,
                        state.OriPos,
                        state.Color,
                        preset.ChangeColor);
                }

                TMPTextMeshUtility.ApplyMesh(_text);
            });

            _fadeTween = phaseTween;
            _tweens.Add(phaseTween);
        }

        private static List<CharState> CreateStates(
            List<TMPTextMeshUtility.CharMeshRef> chars,
            TMPTextEffectPreset preset,
            float startAlpha)
        {
            var states = new List<CharState>(chars.Count);
            for (int i = 0; i < chars.Count; i++)
            {
                TMPTextMeshUtility.CharMeshRef ch = chars[i];
                Color baseColor = preset.ChangeColor ? preset.EndColor : (Color)ch.OriColor;
                states.Add(new CharState
                {
                    MaterialIndex = ch.MaterialIndex,
                    VertexIndex = ch.VertexIndex,
                    OriPos = ch.OriPos,
                    OriColor = ch.OriColor,
                    Pos = Vector3.zero,
                    Color = WithAlpha(baseColor, startAlpha)
                });
            }

            return states;
        }

        private static void ApplyMoveEase(Tweener tweener, TMPTextEffectPreset preset)
        {
            if (preset.UseAnimationCurve && preset.MoveCurve != null)
            {
                tweener.SetEase(preset.MoveCurve);
            }
            else
            {
                tweener.SetEase(preset.MoveEase);
            }
        }

        private static void ApplyFadeEase(Tweener tweener, TMPTextEffectPreset preset)
        {
            if (preset.UseAnimationCurve && preset.FadeCurve != null)
            {
                tweener.SetEase(preset.FadeCurve);
            }
            else
            {
                tweener.SetEase(preset.FadeEase);
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private sealed class CharState
        {
            public int MaterialIndex;
            public int VertexIndex;
            public Vector3[] OriPos;
            public Color32 OriColor;
            public Vector3 Pos;
            public Color Color;
        }
    }
}
