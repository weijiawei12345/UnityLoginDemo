using GameUI.TextFx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局等待覆盖层。由异步调用成对调用 Show/Hide，避免请求期间发生重复操作。
/// 动效优先使用 Inspector 指定的 Preset Asset；未指定时可走 Resources 或内置模板。
/// </summary>
public sealed class LoadingOverlayView : MonoBehaviour
{
    private const string DefaultResourcesPresetPath = "TextFx/LoadingOverlayEffect";

    private static LoadingOverlayView _instance;

    [Header("TextFx")]
    [Tooltip("用 Tools/TextFx/Effect Preview 调好并保存的预设。优先使用此项。")]
    [SerializeField] private TMPTextEffectPresetAsset _effectPresetAsset;

    [Tooltip("未指定 Preset Asset 时，使用内置模板类型。")]
    [SerializeField] private TMPTextEffectType _loadingEffectType = TMPTextEffectType.BounceJump;

    [SerializeField] private string _loadingTextContent = "Loading...";

    [Tooltip("若未拖 Asset，尝试从 Resources/TextFx/LoadingOverlayEffect 加载。")]
    [SerializeField] private bool _tryLoadFromResources = true;

    private Transform _uiRoot;
    private GameObject _overlay;
    private CanvasGroup _canvasGroup;
    private TMP_Text _loadingText;
    private TMPTextAnimator _textAnimator;
    private int _visibleCount;

    public TMPTextEffectType LoadingEffectType
    {
        get => _loadingEffectType;
        set => _loadingEffectType = value;
    }

    public TMPTextEffectPresetAsset EffectPresetAsset
    {
        get => _effectPresetAsset;
        set => _effectPresetAsset = value;
    }

    public static LoadingOverlayView GetOrCreate(Transform uiRoot)
    {
        if (uiRoot == null)
        {
            return null;
        }

        if (_instance == null)
        {
            _instance = uiRoot.GetComponent<LoadingOverlayView>();
            if (_instance == null)
            {
                _instance = uiRoot.gameObject.AddComponent<LoadingOverlayView>();
            }
        }

        _instance._uiRoot = uiRoot;
        return _instance;
    }

    private void OnDestroy()
    {
        StopAnimation();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void Show()
    {
        _visibleCount++;
        if (!CreateOverlayIfNeeded())
        {
            return;
        }

        _overlay.transform.SetAsLastSibling();
        _overlay.SetActive(true);
        _canvasGroup.blocksRaycasts = true;
        StartAnimation();
    }

    public void Hide()
    {
        _visibleCount = Mathf.Max(0, _visibleCount - 1);
        if (_visibleCount > 0 || _overlay == null)
        {
            return;
        }

        StopAnimation();
        _canvasGroup.blocksRaycasts = false;
        _overlay.SetActive(false);
    }

    private bool CreateOverlayIfNeeded()
    {
        if (_overlay != null)
        {
            return true;
        }

        if (_uiRoot == null)
        {
            Debug.LogError("[Loading] UI root is not configured.");
            return false;
        }

        _overlay = new GameObject(
            "LoadingOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        _overlay.transform.SetParent(_uiRoot, false);

        RectTransform overlayRect = _overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = _overlay.GetComponent<Image>();
        overlayImage.color = new Color(0.03f, 0.05f, 0.08f, 0.72f);

        _canvasGroup = _overlay.GetComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject(
            "LoadingText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_overlay.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(500f, 90f);

        _loadingText = textObject.GetComponent<TextMeshProUGUI>();
        _loadingText.font = TMP_Settings.defaultFontAsset;
        _loadingText.fontSize = 85f;
        _loadingText.fontStyle = FontStyles.Bold;
        _loadingText.alignment = TextAlignmentOptions.Center;
        _loadingText.color = Color.white;
        _loadingText.raycastTarget = false;
        // 避免 Overflow=Ellipsis 时把末尾 "..." 裁掉或替换成不可见省略符
        _loadingText.enableWordWrapping = false;
        _loadingText.overflowMode = TextOverflowModes.Overflow;

        _textAnimator = textObject.AddComponent<TMPTextAnimator>();

        _overlay.SetActive(false);
        return true;
    }

    private void StartAnimation()
    {
        StopAnimation();
        if (_textAnimator == null || _loadingText == null)
        {
            return;
        }

        TMPTextEffectPreset preset = ResolvePreset();
        _textAnimator.PlayLoop(preset);
    }

    private void StopAnimation()
    {
        _textAnimator?.Stop();
    }

    private TMPTextEffectPreset ResolvePreset()
    {
        TMPTextEffectPresetAsset asset = _effectPresetAsset;
        if (asset == null && _tryLoadFromResources)
        {
            asset = Resources.Load<TMPTextEffectPresetAsset>(DefaultResourcesPresetPath);
        }

        if (asset != null)
        {
            TMPTextEffectPreset fromAsset = asset.CreateRuntimeCopy();

            // 加载页文案以 LoadingOverlayView 配置为准，避免 Asset 里的 "Loading" 覆盖掉 "Loading..."
            if (!string.IsNullOrEmpty(_loadingTextContent))
            {
                fromAsset.DisplayText = _loadingTextContent;
                if (fromAsset.EffectType == TMPTextEffectType.LoadingDots)
                {
                    fromAsset.DotsBaseText = TrimTrailingDots(_loadingTextContent);
                }
            }
            else if (fromAsset.EffectType == TMPTextEffectType.LoadingDots)
            {
                string dotsBase = string.IsNullOrEmpty(fromAsset.DotsBaseText)
                    ? fromAsset.DisplayText
                    : fromAsset.DotsBaseText;
                fromAsset.DotsBaseText = TrimTrailingDots(dotsBase);
            }

            return fromAsset;
        }

        return BuildBuiltinPreset(_loadingEffectType, _loadingTextContent);
    }

    private static string TrimTrailingDots(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "Loading";
        }

        // 去掉末尾 ASCII "." 与 Unicode 省略号 "…"，避免 LoadingDots 叠加成 Loading......
        return text.TrimEnd('.', '…', '．');
    }

    private static TMPTextEffectPreset BuildBuiltinPreset(TMPTextEffectType type, string text)
    {
        string content = string.IsNullOrEmpty(text) ? "Loading" : text;
        switch (type)
        {
            case TMPTextEffectType.LoadingDots:
                return TMPTextEffectPreset.DefaultLoadingDots(TrimTrailingDots(content));
            case TMPTextEffectType.TypewriterFade:
                return TMPTextEffectPreset.DefaultTypewriter(content);
            case TMPTextEffectType.Wave:
                return TMPTextEffectPreset.DefaultWave(content);
            default:
                return TMPTextEffectPreset.DefaultBounceJump(content);
        }
    }
}
