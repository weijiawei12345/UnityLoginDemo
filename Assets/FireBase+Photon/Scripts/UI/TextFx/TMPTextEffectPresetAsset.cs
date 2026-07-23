using UnityEngine;

namespace GameUI.TextFx
{
    /// <summary>
    /// 可保存的文字动效预设资源，供编辑器窗口与运行时共用。
    /// </summary>
    [CreateAssetMenu(
        fileName = "TMPTextEffectPreset",
        menuName = "UI/TextFx/Effect Preset",
        order = 10)]
    public sealed class TMPTextEffectPresetAsset : ScriptableObject
    {
        public TMPTextEffectPreset Preset = new TMPTextEffectPreset();

        public TMPTextEffectPreset CreateRuntimeCopy()
        {
            return Preset != null ? Preset.Clone() : TMPTextEffectPreset.DefaultBounceJump("Loading");
        }
    }
}
