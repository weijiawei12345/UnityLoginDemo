using ARPG.Auth;
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
    private AuthFormBindings _formBindings;
    private AuthLoginFlowCoordinator _loginFlowCoordinator;
    private UsernamePanelView _usernamePanelView;
    private LoadingOverlayView _loadingOverlay;
    private bool _isFirebaseReady;
    private bool _isSubmitting;

    private void Awake()
    {
        AutoBindIfNeeded();
        _formBindings = new AuthFormBindings(
            _loginPanel,
            _registerPanel,
            _loginUserNameInput,
            _loginPasswordInput,
            _registerUserNameInput,
            _registerPasswordInput,
            _confirmPasswordInput,
            _loginButton,
            _goRegisterButton,
            _registerButton,
            _goLoginButton,
            _loginAccountTip,
            _loginPasswordTip,
            _registerAccountTip,
            _registerPasswordTip,
            _confirmPasswordTip,
            _statusText);
        _formBindings.ConfigurePasswordFields();
        _formBindings.HideAllInputTips();
        _usernamePanelView = UsernamePanelView.GetOrCreate(transform);
        _loadingOverlay = LoadingOverlayView.GetOrCreate(transform);
        _loginFlowCoordinator = new AuthLoginFlowCoordinator(
            _usernamePanelView,
            _loadingOverlay,
            SetSubmitting,
            SetStatus);
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

        _formBindings.BindTipClearEvents();
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

        _formBindings.UnbindTipClearEvents();
    }

    // Unity 生命周期函数只能使用 void；异步初始化逻辑在此边界内等待完成。
    private async void Start()
    {
        // UsernamePanel 只在认证成功且 Firestore 中没有昵称时才从 Resources 加载。
        _usernamePanelView.Hide();
        ShowLogin();
        _formBindings.SetInteractable(false);

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
        _formBindings.SetInteractable(_isFirebaseReady);

        if (!_isFirebaseReady)
        {
            _formBindings.SetStatus(initialization.Message);
        }
        else
        {
            string kickMessage = AuthKickNotice.Consume();
            if (!string.IsNullOrEmpty(kickMessage))
            {
                _formBindings.ShowResult(AuthResult.Fail(kickMessage, AuthField.Email), isLogin: true);
            }
        }
    }

    public void ShowLogin()
    {
        Debug.Log("[Login] Switch to LoginPanel");
        _formBindings.ShowLoginPanel();
    }

    public void ShowRegister()
    {
        Debug.Log("[Login] Switch to RegisterPanel");
        _formBindings.ShowRegisterPanel();
    }

    // Unity Button 回调必须使用 void；认证请求在回调内部异步执行。
    public async void OnLoginClicked()
    {
        if (!_isFirebaseReady || _isSubmitting)
        {
            return;
        }

        _formBindings.HideLoginInputTips();
        SetSubmitting(true);

        _loadingOverlay.Show();
        AuthResult result;
        try
        {
            result = await _authController.LoginAsync(_formBindings.CreateLoginRequest());
        }
        finally
        {
            _loadingOverlay.Hide();
            SetSubmitting(false);
        }

        ShowLoginResult(result);
        if (result.Success)
        {
            _loginFlowCoordinator.ContinueAfterLogin();
        }
    }

    // Unity Button 回调必须使用 void；注册请求在回调内部异步执行。
    public async void OnRegisterClicked()
    {
        if (!_isFirebaseReady || _isSubmitting)
        {
            return;
        }

        _formBindings.HideRegisterInputTips();
        SetSubmitting(true);

        string registeredEmail = _formBindings.RegisterEmail;
        _loadingOverlay.Show();
        AuthResult result;
        try
        {
            result = await _authController.RegisterAsync(_formBindings.CreateRegisterRequest());
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
            _formBindings.SetLoginEmail(registeredEmail);
            _formBindings.SetStatus(result.Message);
        }
    }

    private void ShowLoginResult(AuthResult result)
    {
        _formBindings.ShowResult(result, isLogin: true);
        if (result.Success && result.User != null)
        {
            Debug.Log($"[Auth] Authenticated player: {result.User.Name} ({result.User.Uid})");
        }
    }

    private void ShowRegisterResult(AuthResult result)
    {
        _formBindings.ShowResult(result, isLogin: false);
        if (result.Success && result.User != null)
        {
            Debug.Log($"[Auth] Authenticated player: {result.User.Name} ({result.User.Uid})");
        }
    }

    private void SetStatus(string message)
    {
        _formBindings.SetStatus(message);
    }

    // 认证请求期间锁定所有认证按钮，避免用户重复点击创建多个 Firebase 请求。
    private void SetSubmitting(bool submitting)
    {
        _isSubmitting = submitting;
        _formBindings.SetInteractable(_isFirebaseReady && !submitting);
    }

    // 当前 LoginMenu 依赖运行时路径绑定以兼容已有场景；本次只隔离该技术债，不重命名节点或修改序列化引用。
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
