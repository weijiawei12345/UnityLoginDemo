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

            var states = CreateStates(_chars, preset, startAlpha: 0f);
            _masterSequence = DOTween.Sequence().SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale).SetTarget(_text);

            for (int i = 0; i < states.Count; i++)
            {
                CharState state = states[i];
                float at = i * preset.CharDelay;
                Sequence charSeq = DOTween.Sequence().SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale);

                Vector3 endPos = new Vector3(0f, preset.JumpHeight, 0f);
                Color endColor = preset.ChangeColor ? preset.EndColor : WithAlpha(state.Color, 1f);
                Color startColor = WithAlpha(endColor, 0f);
                state.Color = startColor;
                state.Pos = Vector3.zero;

                Tweener move = DOTween.To(() => state.Pos, x => state.Pos = x, endPos, preset.Duration)
                    .SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale);
                ApplyMoveEase(move, preset);

                // Jump up then return to origin within same duration via curve, or yoyo half.
                if (!preset.UseAnimationCurve)
                {
                    move.SetLoops(2, LoopType.Yoyo);
                }

                Tweener fadeIn = DOTween.To(() => state.Color, x => state.Color = x, endColor, preset.FadeInDuration)
                    .SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale);
                ApplyFadeEase(fadeIn, preset);

                charSeq.Insert(0f, move);
                charSeq.Insert(0f, fadeIn);

                if (preset.FadeOutDuration > 0f)
                {
                    Color fadeOutColor = WithAlpha(endColor, 0f);
                    Tweener fadeOut = DOTween.To(() => state.Color, x => state.Color = x, fadeOutColor, preset.FadeOutDuration)
                        .SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale);
                    ApplyFadeEase(fadeOut, preset);
                    charSeq.Insert(Mathf.Max(preset.Duration, preset.FadeInDuration), fadeOut);
                }

                charSeq.OnUpdate(() =>
                {
                    TMPTextMeshUtility.SetVertexPosition(
                        _text,
                        state.MaterialIndex,
                        state.VertexIndex,
                        state.Pos,
                        state.OriPos,
                        state.Color,
                        preset.ChangeColor);
                    TMPTextMeshUtility.ApplyMesh(_text);
                });

                _masterSequence.Insert(at, charSeq);
                _tweens.Add(charSeq);
            }

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
            _masterSequence = DOTween.Sequence().SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale).SetTarget(_text);

            // Hide all verts immediately.
            for (int i = 0; i < states.Count; i++)
            {
                CharState state = states[i];
                state.Color = WithAlpha(state.Color, 0f);
                TMPTextMeshUtility.SetVertexPosition(
                    _text, state.MaterialIndex, state.VertexIndex, Vector3.zero, state.OriPos, state.Color, true);
            }

            TMPTextMeshUtility.ApplyMesh(_text);

            for (int i = 0; i < states.Count; i++)
            {
                CharState state = states[i];
                float at = i * preset.CharDelay;
                Color endColor = preset.ChangeColor ? preset.EndColor : WithAlpha(state.OriColor, 1f);

                Tweener fadeIn = DOTween.To(() => state.Color, x => state.Color = x, endColor, preset.FadeInDuration)
                    .SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale);
                ApplyFadeEase(fadeIn, preset);

                Sequence charSeq = DOTween.Sequence().SetUpdate(_manualUpdate ? UpdateType.Manual : UpdateType.Normal, preset.IgnoreTimeScale);
                charSeq.Insert(0f, fadeIn);
                charSeq.OnUpdate(() =>
                {
                    TMPTextMeshUtility.SetVertexPosition(
                        _text,
                        state.MaterialIndex,
                        state.VertexIndex,
                        Vector3.zero,
                        state.OriPos,
                        state.Color,
                        true);
                    TMPTextMeshUtility.ApplyMesh(_text);
                });

                _masterSequence.Insert(at, charSeq);
                _tweens.Add(charSeq);
            }

            if (loop)
            {
                _masterSequence.SetLoops(-1, LoopType.Restart);
            }
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
