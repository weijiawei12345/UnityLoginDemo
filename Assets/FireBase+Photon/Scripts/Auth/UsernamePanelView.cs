using System;
using ARPG.Auth;
using ARPG.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UsernamePanel 的 View 层：显示昵称面板和转发按钮事件，不直接读写 Firestore。
/// </summary>
public sealed class UsernamePanelView : MonoBehaviour
{
    private static UsernamePanelView _instance;
    private const string UsernamePanelPrefabPath = "Prefabs/UI/UsernamePanel";

    private readonly UserNameController _userNameController = new UserNameController();
    private readonly PlayerNameSyncController _playerNameSyncController = new PlayerNameSyncController();

    private Transform _uiRoot;
    private GameObject _usernamePanel;
    private TMP_InputField _nameInput;
    private Button _confirmButton;
    private TMP_Text _statusText;
    private TMP_Text _inputTip;
    private LoadingOverlayView _loadingOverlay;
    private bool _isSaving;
    private bool _renameMode;
    private Action _onProfileReadyForPlay;

    public static UsernamePanelView GetOrCreate(Transform uiRoot)
    {
        if (uiRoot == null)
        {
            return null;
        }

        if (_instance == null)
        {
            _instance = uiRoot.GetComponent<UsernamePanelView>();
            if (_instance == null)
            {
                _instance = uiRoot.gameObject.AddComponent<UsernamePanelView>();
            }
        }

        _instance.Initialize(uiRoot);
        return _instance;
    }

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Initialize(Transform uiRoot)
    {
        _uiRoot = uiRoot;
        _statusText = GetDirectChildComponent<TMP_Text>(_uiRoot, "StatusText");
        _loadingOverlay = LoadingOverlayView.GetOrCreate(_uiRoot);
    }

    /// <summary>
    /// 认证成功后调用。已有昵称则立刻回调；没有则弹面板，保存成功后再回调（用于进入 Play）。
    /// Firestore 托管失败时：若会话里已有可用展示名则降级进 Play，避免卡在登录页。
    /// 注意：原生层硬崩无法被 try/catch 捕获，依赖 Repository 关闭 Persistence。
    /// </summary>
    /// <param name="onProfileReadyForPlay">昵称就绪，可进 Play</param>
    /// <param name="onAborted">无法自动继续（需用户停留在登录流程）</param>
    public async void CheckCurrentPlayerNameAsync(
        Action onProfileReadyForPlay = null,
        Action onAborted = null)
    {
        Hide();
        _onProfileReadyForPlay = onProfileReadyForPlay;

        Debug.Log("[Profile] CheckCurrentPlayerNameAsync begin.");
        _loadingOverlay.Show();
        UserNameResult result;
        try
        {
            result = await _userNameController.LoadCurrentPlayerNameAsync();
        }
        finally
        {
            _loadingOverlay.Hide();
        }

        Debug.Log($"[Profile] Load finished. success={result.Success}, hasName={result.HasName}, message={result.Message}");

        if (!result.Success)
        {
            // 托管失败：尽量用会话名进游戏，而不是卡死在登录页。
            if (HasUsableSessionName())
            {
                Debug.LogWarning($"[Profile] Firestore load failed, fallback to session name and enter Play. reason={result.Message}");
                SetStatus(result.Message);
                NotifyProfileReadyForPlay();
                return;
            }

            SetStatus(result.Message);
            _onProfileReadyForPlay = null;
            onAborted?.Invoke();
            return;
        }

        if (result.HasName)
        {
            NotifyProfileReadyForPlay();
            return;
        }

        // 需要用户首次起名：解锁登录 UI 提交锁，只保留昵称面板。
        onAborted?.Invoke();
        _renameMode = false;
        Show();
    }

    private static bool HasUsableSessionName()
    {
        return UserSession.IsLoggedIn
            && UserSession.Current != null
            && !string.IsNullOrWhiteSpace(UserSession.Current.Name);
    }

    public void Hide()
    {
        if (_usernamePanel != null)
        {
            _usernamePanel.SetActive(false);
        }

        _renameMode = false;
    }

    /// <summary>
    /// Play 场景改名：复用 UsernamePanel，确认后持久化并同步 NetworkPlayer。
    /// </summary>
    public void ShowForRename()
    {
        _renameMode = true;
        Show();

        if (_nameInput == null)
        {
            return;
        }

        bool hasSessionName = UserSession.IsLoggedIn
            && UserSession.Current != null
            && !string.IsNullOrWhiteSpace(UserSession.Current.Name);

        _nameInput.text = hasSessionName
            ? UserSession.Current.Name.Trim()
            : string.Empty;

        _nameInput.Select();
        _nameInput.ActivateInputField();
    }

    private void Show()
    {
        if (!LoadPanelIfNeeded())
        {
            return;
        }

        SetStatus(string.Empty);
        _usernamePanel.SetActive(true);
        SetButtonInteractable(true);

        if (!_renameMode)
        {
            _nameInput.text = string.Empty;
        }

        _nameInput.Select();
        _nameInput.ActivateInputField();
    }

    private async void OnConfirmClicked()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        SetButtonInteractable(false);

        _loadingOverlay.Show();
        UserNameResult result;
        try
        {
            result = _renameMode
                ? await _playerNameSyncController.RenameAndSyncAsync(_nameInput.text)
                : await _userNameController.SaveCurrentPlayerNameAsync(_nameInput.text);
        }
        finally
        {
            _loadingOverlay.Hide();
            _isSaving = false;
        }

        if (!result.Success)
        {
            SetButtonInteractable(true);
            SetStatus(result.Message);
            return;
        }

        Debug.Log($"[Profile] Player name saved: {result.Name}");
        bool shouldEnterPlay = !_renameMode;
        Hide();

        if (shouldEnterPlay)
        {
            NotifyProfileReadyForPlay();
        }
    }

    private void NotifyProfileReadyForPlay()
    {
        Action callback = _onProfileReadyForPlay;
        _onProfileReadyForPlay = null;
        callback?.Invoke();
    }

    // 仅在确实需要设置昵称时加载预制体，场景中不保留 UsernamePanel 实例。
    private bool LoadPanelIfNeeded()
    {
        if (_usernamePanel != null)
        {
            return true;
        }

        if (_uiRoot == null)
        {
            SetStatus("UI root is not configured.");
            return false;
        }

        GameObject panelPrefab = Resources.Load<GameObject>(UsernamePanelPrefabPath);
        if (panelPrefab == null)
        {
            SetStatus("Username panel prefab was not found.");
            return false;
        }

        _usernamePanel = Instantiate(panelPrefab, _uiRoot);
        _usernamePanel.name = panelPrefab.name;
        _usernamePanel.SetActive(false);

        _nameInput = _usernamePanel.GetComponentInChildren<TMP_InputField>(true);
        _inputTip = FindChildComponentByName<TMP_Text>(_usernamePanel.transform, "InputTip");
        Button[] buttons = _usernamePanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == "OK")
            {
                _confirmButton = button;
                break;
            }
        }

        if (_nameInput == null || _confirmButton == null)
        {
            Destroy(_usernamePanel);
            _usernamePanel = null;
            _nameInput = null;
            _confirmButton = null;
            SetStatus("Username panel prefab is incomplete.");
            return false;
        }

        _confirmButton.onClick.AddListener(OnConfirmClicked);
        return true;
    }

    private void SetStatus(string message)
    {
        string text = message ?? string.Empty;
        bool hasMessage = !string.IsNullOrEmpty(text);

        if (_statusText != null)
        {
            _statusText.text = text;
            _statusText.gameObject.SetActive(hasMessage);
        }

        if (_inputTip != null)
        {
            _inputTip.text = text;
            _inputTip.gameObject.SetActive(hasMessage);
        }
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (_confirmButton != null)
        {
            _confirmButton.interactable = interactable;
        }
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static T GetDirectChildComponent<T>(Transform parent, string childName) where T : Component
    {
        Transform child = FindDirectChild(parent, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        foreach (T component in components)
        {
            if (component.name == childName)
            {
                return component;
            }
        }

        return null;
    }
}
