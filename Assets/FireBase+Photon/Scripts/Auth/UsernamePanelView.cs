using System;
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

    private readonly UserNameController _userNameController = new UserNameController();

    private GameObject _usernamePanel;
    private TMP_InputField _nameInput;
    private Button _confirmButton;
    private TMP_Text _statusText;
    private bool _isSaving;

    public static UsernamePanelView Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
            {
                throw new InvalidOperationException("Canvas was not found for UsernamePanelView.");
            }

            _instance = canvasObject.GetComponent<UsernamePanelView>();
            if (_instance == null)
            {
                _instance = canvasObject.AddComponent<UsernamePanelView>();
            }

            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
        AutoBind();
        Hide();
    }

    private void OnEnable()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }
    }

    private void OnDisable()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }
    }

    /// <summary>
    /// 认证成功后调用。只有 Firestore 中没有有效昵称时才显示面板。
    /// </summary>
    public async void CheckCurrentPlayerNameAsync()
    {
        Hide();

        UserNameResult result = await _userNameController.LoadCurrentPlayerNameAsync();
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
        if (_usernamePanel == null || _nameInput == null || _confirmButton == null)
        {
            SetStatus("Username panel is not configured.");
            return;
        }

        SetStatus(string.Empty);
        _usernamePanel.SetActive(true);
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

        UserNameResult result = await _userNameController.SaveCurrentPlayerNameAsync(_nameInput.text);
        _isSaving = false;

        if (!result.Success)
        {
            SetButtonInteractable(true);
            SetStatus(result.Message);
            return;
        }

        Debug.Log($"[Profile] Player name saved: {result.Name}");
        Hide();
    }

    private void AutoBind()
    {
        Transform panelTransform = FindDirectChild(transform, "UsernamePanel");
        _usernamePanel = panelTransform != null ? panelTransform.gameObject : null;
        _statusText = GetDirectChildComponent<TMP_Text>(transform, "StatusText");

        if (_usernamePanel == null)
        {
            return;
        }

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
