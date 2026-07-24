using ARPG.Auth;
using ARPG.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Auth UI View：绑定 LoginPanel / RegisterPanel，转发按钮事件给 AuthController。
/// </summary>
public class AuthUIView : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject _loginPanel;
    [SerializeField] private GameObject _registerPanel;

    [Header("Login")]
    [SerializeField] private TMP_InputField _loginUserNameInput;
    [SerializeField] private TMP_InputField _loginPasswordInput;
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _goRegisterButton;
    [SerializeField] private TMP_Text _loginAccountTip;
    [SerializeField] private TMP_Text _loginPasswordTip;

    [Header("Register")]
    [SerializeField] private TMP_InputField _registerUserNameInput;
    [SerializeField] private TMP_InputField _registerPasswordInput;
    [SerializeField] private TMP_InputField _confirmPasswordInput;
    [SerializeField] private Button _registerButton;
    [SerializeField] private Button _goLoginButton;
    [SerializeField] private TMP_Text _registerAccountTip;
    [SerializeField] private TMP_Text _registerPasswordTip;
    [SerializeField] private TMP_Text _confirmPasswordTip;

    [Header("Feedback")]
    [SerializeField] private TMP_Text _statusText;

    private readonly AuthController _authController = new AuthController();
    private UsernamePanelView _usernamePanelView;
    private LoadingOverlayView _loadingOverlay;
    private bool _isFirebaseReady;
    private bool _isSubmitting;

    private void Awake()
    {
        AutoBindIfNeeded();
        ConfigurePasswordField(_loginPasswordInput);
        ConfigurePasswordField(_registerPasswordInput);
        ConfigurePasswordField(_confirmPasswordInput);
        HideAllInputTips();
        _usernamePanelView = UsernamePanelView.GetOrCreate(transform);
        _loadingOverlay = LoadingOverlayView.GetOrCreate(transform);
    }

    private void OnEnable()
    {
        if (_loginButton != null)
        {
            _loginButton.onClick.AddListener(OnLoginClicked);
        }

        if (_goRegisterButton != null)
        {
            _goRegisterButton.onClick.AddListener(ShowRegister);
        }

        if (_registerButton != null)
        {
            _registerButton.onClick.AddListener(OnRegisterClicked);
        }

        if (_goLoginButton != null)
        {
            _goLoginButton.onClick.AddListener(ShowLogin);
        }

        BindInputSelect(_loginUserNameInput, HideLoginAccountTip);
        BindInputSelect(_loginPasswordInput, HideLoginPasswordTip);
        BindInputSelect(_registerUserNameInput, HideRegisterAccountTip);
        BindInputSelect(_registerPasswordInput, HideRegisterPasswordTip);
        BindInputSelect(_confirmPasswordInput, HideConfirmPasswordTip);
    }

    private void OnDisable()
    {
        if (_loginButton != null)
        {
            _loginButton.onClick.RemoveListener(OnLoginClicked);
        }

        if (_goRegisterButton != null)
        {
            _goRegisterButton.onClick.RemoveListener(ShowRegister);
        }

        if (_registerButton != null)
        {
            _registerButton.onClick.RemoveListener(OnRegisterClicked);
        }

        if (_goLoginButton != null)
        {
            _goLoginButton.onClick.RemoveListener(ShowLogin);
        }

        UnbindInputSelect(_loginUserNameInput, HideLoginAccountTip);
        UnbindInputSelect(_loginPasswordInput, HideLoginPasswordTip);
        UnbindInputSelect(_registerUserNameInput, HideRegisterAccountTip);
        UnbindInputSelect(_registerPasswordInput, HideRegisterPasswordTip);
        UnbindInputSelect(_confirmPasswordInput, HideConfirmPasswordTip);
    }

    private async void Start()
    {
        // UsernamePanel 只在认证成功且 Firestore 中没有昵称时才从 Resources 加载。
        _usernamePanelView.Hide();
        ShowLogin();
        SetAuthButtonsInteractable(false);

        _loadingOverlay.Show();
        FirebaseInitializationResult initialization;
        try
        {
            initialization = await FirebaseAuthManager.Instance.InitializeAsync();
        }
        finally
        {
            _loadingOverlay.Hide();
        }

        _isFirebaseReady = initialization.Success;
        SetAuthButtonsInteractable(_isFirebaseReady);

        if (!_isFirebaseReady)
        {
            SetStatus(initialization.Message);
        }
    }

    public void ShowLogin()
    {
        Debug.Log("[Login] Switch to LoginPanel");
        ClearRegisterInputs();
        HideRegisterInputTips();

        if (_loginPanel != null)
        {
            _loginPanel.SetActive(true);
        }

        if (_registerPanel != null)
        {
            _registerPanel.SetActive(false);
        }

        SetStatus(string.Empty);
    }

    public void ShowRegister()
    {
        Debug.Log("[Login] Switch to RegisterPanel");
        ClearLoginInputs();
        HideLoginInputTips();

        if (_loginPanel != null)
        {
            _loginPanel.SetActive(false);
        }

        if (_registerPanel != null)
        {
            _registerPanel.SetActive(true);
        }

        SetStatus(string.Empty);
    }

    private void ClearLoginInputs()
    {
        ClearInput(_loginUserNameInput);
        ClearInput(_loginPasswordInput);
    }

    private void ClearRegisterInputs()
    {
        ClearInput(_registerUserNameInput);
        ClearInput(_registerPasswordInput);
        ClearInput(_confirmPasswordInput);
    }

    private static void ClearInput(TMP_InputField field)
    {
        if (field == null)
        {
            return;
        }

        field.text = string.Empty;
        field.DeactivateInputField();
    }

    public async void OnLoginClicked()
    {
        if (!_isFirebaseReady || _isSubmitting)
        {
            return;
        }

        HideLoginInputTips();
        SetSubmitting(true);

        _loadingOverlay.Show();
        AuthResult result;
        try
        {
            result = await _authController.LoginAsync(new LoginRequest
            {
                Email = _loginUserNameInput != null ? _loginUserNameInput.text : string.Empty,
                Password = _loginPasswordInput != null ? _loginPasswordInput.text : string.Empty
            });
        }
        finally
        {
            _loadingOverlay.Hide();
            SetSubmitting(false);
        }

        ShowLoginResult(result);
        if (result.Success)
        {
            SetSubmitting(true);
            Debug.Log("[Login] Auth success, start profile check before Play.");
            _usernamePanelView.CheckCurrentPlayerNameAsync(
                EnterPlayScene,
                () => SetSubmitting(false));
        }
    }

    private async void EnterPlayScene()
    {
        Debug.Log("[Login] EnterPlayScene begin.");
        if (_loadingOverlay != null)
        {
            _loadingOverlay.Show();
        }

        try
        {
            await GameSceneController.LoadPlaySceneAsync();
            Debug.Log("[Login] EnterPlayScene load requested.");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            if (_loadingOverlay != null)
            {
                _loadingOverlay.Hide();
            }

            SetSubmitting(false);
            SetStatus("Failed to enter the game scene.");
        }
    }

    public async void OnRegisterClicked()
    {
        if (!_isFirebaseReady || _isSubmitting)
        {
            return;
        }

        HideRegisterInputTips();
        SetSubmitting(true);

        string registeredEmail = _registerUserNameInput != null ? _registerUserNameInput.text.Trim() : string.Empty;
        _loadingOverlay.Show();
        AuthResult result;
        try
        {
            result = await _authController.RegisterAsync(new RegisterRequest
            {
                Email = registeredEmail,
                Password = _registerPasswordInput != null ? _registerPasswordInput.text : string.Empty,
                ConfirmPassword = _confirmPasswordInput != null ? _confirmPasswordInput.text : string.Empty
            });
        }
        finally
        {
            _loadingOverlay.Hide();
            SetSubmitting(false);
        }

        ShowRegisterResult(result);
        if (result.Success)
        {
            ShowLogin();
            if (_loginUserNameInput != null)
            {
                _loginUserNameInput.text = registeredEmail;
            }

            SetStatus(result.Message);
        }
    }

    private void ShowLoginResult(AuthResult result)
    {
        if (result.Success)
        {
            HideLoginInputTips();
            SetStatus(result.Message);
            if (result.User != null)
            {
                Debug.Log($"[Auth] Authenticated player: {result.User.Name} ({result.User.Uid})");
            }

            return;
        }

        SetStatus(string.Empty);
        ShowFieldTip(result.Field, result.Message, isLogin: true);
    }

    private void ShowRegisterResult(AuthResult result)
    {
        if (result.Success)
        {
            HideRegisterInputTips();
            if (result.User != null)
            {
                Debug.Log($"[Auth] Authenticated player: {result.User.Name} ({result.User.Uid})");
            }

            return;
        }

        SetStatus(string.Empty);
        ShowFieldTip(result.Field, result.Message, isLogin: false);
    }

    private void ShowFieldTip(AuthField field, string message, bool isLogin)
    {
        switch (field)
        {
            case AuthField.Email:
                SetInputTip(isLogin ? _loginAccountTip : _registerAccountTip, message);
                break;
            case AuthField.Password:
                SetInputTip(isLogin ? _loginPasswordTip : _registerPasswordTip, message);
                break;
            case AuthField.ConfirmPassword:
                SetInputTip(_confirmPasswordTip, message);
                break;
            default:
                SetStatus(message);
                break;
        }
    }

    private static void SetInputTip(TMP_Text tip, string message)
    {
        if (tip == null)
        {
            return;
        }

        tip.text = message ?? string.Empty;
        tip.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private static void HideInputTip(TMP_Text tip)
    {
        if (tip == null)
        {
            return;
        }

        tip.text = string.Empty;
        tip.gameObject.SetActive(false);
    }

    private void HideAllInputTips()
    {
        HideLoginInputTips();
        HideRegisterInputTips();
    }

    private void HideLoginInputTips()
    {
        HideInputTip(_loginAccountTip);
        HideInputTip(_loginPasswordTip);
    }

    private void HideRegisterInputTips()
    {
        HideInputTip(_registerAccountTip);
        HideInputTip(_registerPasswordTip);
        HideInputTip(_confirmPasswordTip);
    }

    private void HideLoginAccountTip(string _)
    {
        HideInputTip(_loginAccountTip);
    }

    private void HideLoginPasswordTip(string _)
    {
        HideInputTip(_loginPasswordTip);
    }

    private void HideRegisterAccountTip(string _)
    {
        HideInputTip(_registerAccountTip);
    }

    private void HideRegisterPasswordTip(string _)
    {
        HideInputTip(_registerPasswordTip);
    }

    private void HideConfirmPasswordTip(string _)
    {
        HideInputTip(_confirmPasswordTip);
    }

    private static void BindInputSelect(TMP_InputField field, UnityEngine.Events.UnityAction<string> onSelect)
    {
        if (field == null)
        {
            return;
        }

        field.onSelect.AddListener(onSelect);
    }

    private static void UnbindInputSelect(TMP_InputField field, UnityEngine.Events.UnityAction<string> onSelect)
    {
        if (field == null)
        {
            return;
        }

        field.onSelect.RemoveListener(onSelect);
    }

    private void SetStatus(string message)
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        _statusText.text = message ?? string.Empty;
    }

    // 认证请求期间锁定所有认证按钮，避免用户重复点击创建多个 Firebase 请求。
    private void SetSubmitting(bool submitting)
    {
        _isSubmitting = submitting;
        SetAuthButtonsInteractable(_isFirebaseReady && !submitting);
    }

    private void SetAuthButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(_loginButton, interactable);
        SetButtonInteractable(_goRegisterButton, interactable);
        SetButtonInteractable(_registerButton, interactable);
        SetButtonInteractable(_goLoginButton, interactable);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void AutoBindIfNeeded()
    {
        if (_loginPanel == null)
        {
            Transform t = FindDirectChild(transform, "LoginPanel");
            if (t != null)
            {
                _loginPanel = t.gameObject;
            }
        }

        if (_registerPanel == null)
        {
            Transform t = FindDirectChild(transform, "RegisterPanel");
            if (t != null)
            {
                _registerPanel = t.gameObject;
            }
        }

        if (_loginUserNameInput == null && _loginPanel != null)
        {
            _loginUserNameInput = FindInput(_loginPanel.transform, "Account/InputField (TMP) (1)/InputField (TMP)");
        }

        if (_loginPasswordInput == null && _loginPanel != null)
        {
            _loginPasswordInput = FindInput(_loginPanel.transform, "Passwords/InputField (TMP) (1)/InputField (TMP)");
        }

        if (_loginAccountTip == null && _loginPanel != null)
        {
            _loginAccountTip = FindTip(_loginPanel.transform, "Account/InputField (TMP) (1)/InputTip");
        }

        if (_loginPasswordTip == null && _loginPanel != null)
        {
            _loginPasswordTip = FindTip(_loginPanel.transform, "Passwords/InputField (TMP) (1)/InputTip");
        }

        if (_loginButton == null && _loginPanel != null)
        {
            _loginButton = FindButton(_loginPanel.transform, "Buttons/login");
        }

        if (_goRegisterButton == null && _loginPanel != null)
        {
            _goRegisterButton = FindButton(_loginPanel.transform, "Buttons/register");
        }

        if (_registerUserNameInput == null && _registerPanel != null)
        {
            _registerUserNameInput = FindInput(_registerPanel.transform, "PanelBg/Account/InputField (TMP) (1)/InputField (TMP)");
        }

        if (_registerPasswordInput == null && _registerPanel != null)
        {
            _registerPasswordInput = FindInput(_registerPanel.transform, "PanelBg/Passwords/InputField (TMP) (1)/InputField (TMP)");
        }

        if (_confirmPasswordInput == null && _registerPanel != null)
        {
            _confirmPasswordInput = FindInput(_registerPanel.transform, "PanelBg/ConfirmPasswords/InputField (TMP) (1)/InputField (TMP)");
        }

        if (_registerAccountTip == null && _registerPanel != null)
        {
            _registerAccountTip = FindTip(_registerPanel.transform, "PanelBg/Account/InputField (TMP) (1)/InputTip");
        }

        if (_registerPasswordTip == null && _registerPanel != null)
        {
            _registerPasswordTip = FindTip(_registerPanel.transform, "PanelBg/Passwords/InputField (TMP) (1)/InputTip");
        }

        if (_confirmPasswordTip == null && _registerPanel != null)
        {
            _confirmPasswordTip = FindTip(_registerPanel.transform, "PanelBg/ConfirmPasswords/InputField (TMP) (1)/InputTip");
        }

        if (_registerButton == null && _registerPanel != null)
        {
            _registerButton = FindButton(_registerPanel.transform, "PanelBg/Buttons/register");
        }

        if (_goLoginButton == null && _registerPanel != null)
        {
            _goLoginButton = FindButton(_registerPanel.transform, "PanelBg/Buttons/login");
        }

        if (_statusText == null)
        {
            Transform status = FindDirectChild(transform, "StatusText");
            if (status != null)
            {
                _statusText = status.GetComponent<TMP_Text>();
            }
        }
    }

    private static void ConfigurePasswordField(TMP_InputField field)
    {
        if (field == null)
        {
            return;
        }

        field.contentType = TMP_InputField.ContentType.Password;
        field.ForceLabelUpdate();
    }

    private static TMP_InputField FindInput(Transform root, string relativePath)
    {
        Transform t = FindDeepChild(root, relativePath);
        return t != null ? t.GetComponent<TMP_InputField>() : null;
    }

    private static TMP_Text FindTip(Transform root, string relativePath)
    {
        Transform t = FindDeepChild(root, relativePath);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static Button FindButton(Transform root, string relativePath)
    {
        Transform t = FindDeepChild(root, relativePath);
        return t != null ? t.GetComponent<Button>() : null;
    }

    /// <summary>
    /// Transform.Find 找不到 inactive 子物体；InputTip / RegisterPanel 默认关闭，需手动深度查找。
    /// </summary>
    private static Transform FindDeepChild(Transform root, string relativePath)
    {
        if (root == null || string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        string[] parts = relativePath.Split('/');
        Transform current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            current = FindDirectChild(current, parts[i]);
            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }
}
