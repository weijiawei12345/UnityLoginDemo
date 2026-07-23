using System;
using DG.Tweening;
using UnityEngine;

namespace GameUI.TextFx
{
    /// <summary>
    /// 可序列化文字动效参数，便于 Inspector 调参与跨 UI 复用。
    /// </summary>
    [Serializable]
    public class TMPTextEffectPreset
    {
        public TMPTextEffectType EffectType = TMPTextEffectType.BounceJump;

        [Tooltip("非空时播放前写入 TMP 文本")]
        public string DisplayText;

        [Header("Timing")]
        public float Duration = 0.35f;
        public float CharDelay = 0.06f;
        public float FadeInDuration = 0.25f;
        public float FadeOutDuration = 0f;
        public float DotsInterval = 0.22f;
        public bool IgnoreTimeScale = true;

        [Header("Motion")]
        public float JumpHeight = 18f;
        public float WaveAmplitude = 10f;
        public float WaveFrequency = 2.2f;
        public bool UseAnimationCurve;
        public AnimationCurve MoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve FadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public Ease MoveEase = Ease.OutQuad;
        public Ease FadeEase = Ease.OutSine;

        [Header("Color")]
        public bool ChangeColor;
        public Color EndColor = Color.white;
        public float PulseMinAlpha = 0.45f;
        public float PulseDuration = 0.65f;

        [Header("LoadingDots")]
        public string DotsBaseText = "Loading";

        public static TMPTextEffectPreset DefaultBounceJump(string text = "Loading")
        {
            return new TMPTextEffectPreset
            {
                EffectType = TMPTextEffectType.BounceJump,
                DisplayText = text,
                Duration = 0.4f,
                CharDelay = 0.07f,
                JumpHeight = 22f,
                FadeInDuration = 0.2f,
                UseAnimationCurve = true,
                MoveCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.35f, 1f),
                    new Keyframe(1f, 0f)),
                IgnoreTimeScale = true
            };
        }

        public static TMPTextEffectPreset DefaultTypewriter(string text)
        {
            return new TMPTextEffectPreset
            {
                EffectType = TMPTextEffectType.TypewriterFade,
                DisplayText = text,
                Duration = 0.2f,
                CharDelay = 0.05f,
                FadeInDuration = 0.2f,
                JumpHeight = 0f,
                IgnoreTimeScale = true
            };
        }

        public static TMPTextEffectPreset DefaultWave(string text)
        {
            return new TMPTextEffectPreset
            {
                EffectType = TMPTextEffectType.Wave,
                DisplayText = text,
                WaveAmplitude = 12f,
                WaveFrequency = 2.5f,
                CharDelay = 0.08f,
                IgnoreTimeScale = true
            };
        }

        public static TMPTextEffectPreset DefaultLoadingDots(string baseText = "Loading")
        {
            return new TMPTextEffectPreset
            {
                EffectType = TMPTextEffectType.LoadingDots,
                DotsBaseText = baseText,
                DisplayText = baseText,
                DotsInterval = 0.22f,
                PulseMinAlpha = 0.45f,
                PulseDuration = 0.65f,
                IgnoreTimeScale = true
            };
        }

        public TMPTextEffectPreset Clone()
        {
            return new TMPTextEffectPreset
            {
                EffectType = EffectType,
                DisplayText = DisplayText,
                Duration = Duration,
                CharDelay = CharDelay,
                FadeInDuration = FadeInDuration,
                FadeOutDuration = FadeOutDuration,
                DotsInterval = DotsInterval,
                IgnoreTimeScale = IgnoreTimeScale,
                JumpHeight = JumpHeight,
                WaveAmplitude = WaveAmplitude,
                WaveFrequency = WaveFrequency,
                UseAnimationCurve = UseAnimationCurve,
                MoveCurve = MoveCurve != null ? new AnimationCurve(MoveCurve.keys) : AnimationCurve.Linear(0f, 0f, 1f, 1f),
                FadeCurve = FadeCurve != null ? new AnimationCurve(FadeCurve.keys) : AnimationCurve.Linear(0f, 0f, 1f, 1f),
                MoveEase = MoveEase,
                FadeEase = FadeEase,
                ChangeColor = ChangeColor,
                EndColor = EndColor,
                PulseMinAlpha = PulseMinAlpha,
                PulseDuration = PulseDuration,
                DotsBaseText = DotsBaseText
            };
        }

        public void CopyFrom(TMPTextEffectPreset other)
        {
            if (other == null)
            {
                return;
            }

            TMPTextEffectPreset c = other.Clone();
            EffectType = c.EffectType;
            DisplayText = c.DisplayText;
            Duration = c.Duration;
            CharDelay = c.CharDelay;
            FadeInDuration = c.FadeInDuration;
            FadeOutDuration = c.FadeOutDuration;
            DotsInterval = c.DotsInterval;
            IgnoreTimeScale = c.IgnoreTimeScale;
            JumpHeight = c.JumpHeight;
            WaveAmplitude = c.WaveAmplitude;
            WaveFrequency = c.WaveFrequency;
            UseAnimationCurve = c.UseAnimationCurve;
            MoveCurve = c.MoveCurve;
            FadeCurve = c.FadeCurve;
            MoveEase = c.MoveEase;
            FadeEase = c.FadeEase;
            ChangeColor = c.ChangeColor;
            EndColor = c.EndColor;
            PulseMinAlpha = c.PulseMinAlpha;
            PulseDuration = c.PulseDuration;
            DotsBaseText = c.DotsBaseText;
        }
    }
}
