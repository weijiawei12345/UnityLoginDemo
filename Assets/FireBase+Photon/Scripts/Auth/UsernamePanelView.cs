using ARPG.Auth;
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

    private Transform _uiRoot;
    private GameObject _usernamePanel;
    private TMP_InputField _nameInput;
    private Button _confirmButton;
    private TMP_Text _statusText;
    private LoadingOverlayView _loadingOverlay;
    private bool _isSaving;

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
    /// 认证成功后调用。只有 Firestore 中没有有效昵称时才显示面板。
    /// </summary>
    public async void CheckCurrentPlayerNameAsync()
    {
        Hide();

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
        if (!result.Success)
        {
            SetStatus(result.Message);
            return;
        }

        if (!result.HasName)
        {
            Show();
        }
    }

    public void Hide()
    {
        if (_usernamePanel != null)
        {
            _usernamePanel.SetActive(false);
        }
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
        _nameInput.text = string.Empty;
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
            result = await _userNameController.SaveCurrentPlayerNameAsync(_nameInput.text);
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
        Hide();
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
        if (_statusText == null)
        {
            return;
        }

        _statusText.text = message ?? string.Empty;
        _statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
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
}
