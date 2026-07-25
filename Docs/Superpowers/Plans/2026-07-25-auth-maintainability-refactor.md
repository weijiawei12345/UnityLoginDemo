# Auth Maintainability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce authentication UI coupling, remove duplicate runtime session state, and add Chinese code comments and deterministic EditMode coverage without changing current login behavior.

**Architecture:** `AuthUIView` remains the Unity event adapter. A pure request validator owns deterministic validation, `AuthFormBindings` owns form values and visual feedback, and `AuthLoginFlowCoordinator` owns the existing successful-login continuation. `UserSession` remains the only runtime user/session store.

**Tech Stack:** Unity 2022.3.62f2c1, C#, TextMeshPro, Firebase Auth/Firestore, Photon Fusion, NUnit, Unity MCP.

---

### Task 1: Extract And Test Deterministic Request Validation

**Files:**

- Create: `Assets/FireBase+Photon/Scripts/Auth/AuthRequestValidator.cs`
- Create: `Assets/FireBase+Photon/Editor/Tests/AuthRequestValidatorTests.cs`
- Modify: `Assets/FireBase+Photon/Scripts/Auth/AuthController.cs:89-124`

- [ ] **Step 1: Write failing EditMode tests for login and registration validation.**

```csharp
using ARPG.Auth;
using NUnit.Framework;

public sealed class AuthRequestValidatorTests
{
    [Test]
    public void ValidateLogin_WhenEmailIsBlank_ReturnsEmailError()
    {
        AuthResult result = AuthRequestValidator.ValidateLogin(
            new LoginRequest { Email = " ", Password = "password" });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(AuthField.Email, result.Field);
        Assert.AreEqual("Please enter your email.", result.Message);
    }

    [Test]
    public void ValidateRegister_WhenConfirmationDiffers_ReturnsConfirmPasswordError()
    {
        AuthResult result = AuthRequestValidator.ValidateRegistration(
            new RegisterRequest { Email = "player@example.com", Password = "secret1", ConfirmPassword = "secret2" });

        Assert.IsFalse(result.Success);
        Assert.AreEqual(AuthField.ConfirmPassword, result.Field);
        Assert.AreEqual("Passwords do not match.", result.Message);
    }

    [Test]
    public void ValidateRegister_WhenRequestIsValid_ReturnsNull()
    {
        AuthResult result = AuthRequestValidator.ValidateRegistration(
            new RegisterRequest { Email = "player@example.com", Password = "secret1", ConfirmPassword = "secret1" });

        Assert.IsNull(result);
    }
}
```

- [ ] **Step 2: Run the specific EditMode tests and verify they fail because the validator does not exist.**

Run through Unity MCP: `unity_test.run` with `mode: "EditMode"` and test names `AuthRequestValidatorTests`.

Expected: compilation failure reporting that `AuthRequestValidator` does not exist.

- [ ] **Step 3: Add the pure validator with Chinese responsibility comments.**

```csharp
using System;

namespace ARPG.Auth
{
    /// <summary>
    /// 认证请求的纯业务校验器。
    /// 不依赖 Unity、Firebase 或网络服务，因此可在 EditMode 中稳定测试。
    /// </summary>
    public static class AuthRequestValidator
    {
        public static AuthResult ValidateLogin(LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return AuthResult.Fail("Please enter your email.", AuthField.Email);
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return AuthResult.Fail("Please enter your password.", AuthField.Password);
            }

            return null;
        }

        public static AuthResult ValidateRegistration(RegisterRequest request)
        {
            AuthResult loginValidation = ValidateLogin(request == null
                ? null
                : new LoginRequest { Email = request.Email, Password = request.Password });
            if (loginValidation != null)
            {
                return loginValidation;
            }

            if (request.Password.Length < 6)
            {
                return AuthResult.Fail("Password must be at least 6 characters.", AuthField.Password);
            }

            if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            {
                return AuthResult.Fail("Passwords do not match.", AuthField.ConfirmPassword);
            }

            return null;
        }
    }
}
```

- [ ] **Step 4: Delegate from `AuthController` and remove its duplicate private validation methods.**

Replace the first statements in `LoginAsync` and `RegisterAsync` with:

```csharp
AuthResult validationResult = AuthRequestValidator.ValidateLogin(request);
```

and:

```csharp
AuthResult validationResult = AuthRequestValidator.ValidateRegistration(request);
```

Delete `ValidateLoginRequest` and `ValidateRegisterRequest`. Do not change their messages or `AuthField` mappings.

- [ ] **Step 5: Re-run the EditMode tests and commit the focused extraction.**

Run through Unity MCP: `unity_test.run` with `mode: "EditMode"` and `testNames: ["AuthRequestValidatorTests"]`.

Expected: three passing tests.

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/Auth/AuthRequestValidator.cs' 'Assets/FireBase+Photon/Scripts/Auth/AuthController.cs' 'Assets/FireBase+Photon/Editor/Tests/AuthRequestValidatorTests.cs'
git commit -m "refactor: extract auth request validation"
```

### Task 2: Make `UserSession` The Single Runtime Session Store

**Files:**

- Modify: `Assets/FireBase+Photon/Scripts/Auth/AuthController.cs:14-17,127-133`
- Modify: `Assets/FireBase+Photon/Scripts/Auth/AuthModels.cs:95-114`

- [ ] **Step 1: Search before removal to confirm `CurrentUser` has no readers outside `AuthController`.**

Run:

```powershell
rg -n "\bCurrentUser\b" Assets/FireBase+Photon/Scripts
```

Expected: only the declaration, assignment, and cleanup in `AuthController`.

- [ ] **Step 2: Remove the duplicate property and assignments.**

Delete:

```csharp
public UserData CurrentUser { get; private set; }
```

Delete these assignments while preserving `UserSession` calls:

```csharp
CurrentUser = null;
CurrentUser = user;
```

- [ ] **Step 3: Clarify `UserSession` ownership in Chinese.**

Replace its XML summary with:

```csharp
/// <summary>
/// 当前进程唯一的认证会话状态。
/// Firebase 用户、Firestore 单点登录租约和网络展示名都从这里读取；
/// 业务代码不得再维护第二份当前用户副本。
/// </summary>
```

Add this comment immediately before `SetUser`:

```csharp
// 登录成功后一次性写入用户与租约 ID，避免调用方分别更新两个状态字段。
```

- [ ] **Step 4: Compile the changed scripts before scene validation.**

Run through Unity MCP: `unity_script.validate` for `Assets/FireBase+Photon/Scripts/Auth/AuthController.cs`, then for `Assets/FireBase+Photon/Scripts/Auth/AuthModels.cs`.

Expected: no diagnostics.

- [ ] **Step 5: Commit the single-source state cleanup.**

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/Auth/AuthController.cs' 'Assets/FireBase+Photon/Scripts/Auth/AuthModels.cs'
git commit -m "refactor: keep auth session state in one store"
```

### Task 3: Extract Form Binding And Presentation From `AuthUIView`

**Files:**

- Create: `Assets/FireBase+Photon/Scripts/Auth/AuthFormBindings.cs`
- Modify: `Assets/FireBase+Photon/Scripts/Auth/AuthUIView.cs:12-207,313-485,587-652`

- [ ] **Step 1: Add a focused binding and presentation helper.**

Create `AuthFormBindings.cs` with the following complete implementation. Its constructor receives existing controls after `AuthUIView.AutoBindIfNeeded()`; it never searches the hierarchy or instantiates UI.

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ARPG.Auth
{
    /// <summary>
    /// 登录与注册表单的引用和显示操作。
    /// 将 Unity 控件细节集中在此处，避免认证流程直接依赖每个 TMP 控件。
    /// </summary>
    internal sealed class AuthFormBindings
    {
        private readonly GameObject _loginPanel;
        private readonly GameObject _registerPanel;
        private readonly TMP_InputField _loginEmailInput;
        private readonly TMP_InputField _loginPasswordInput;
        private readonly TMP_InputField _registerEmailInput;
        private readonly TMP_InputField _registerPasswordInput;
        private readonly TMP_InputField _confirmPasswordInput;
        private readonly Button _loginButton;
        private readonly Button _goRegisterButton;
        private readonly Button _registerButton;
        private readonly Button _goLoginButton;
        private readonly TMP_Text _loginEmailTip;
        private readonly TMP_Text _loginPasswordTip;
        private readonly TMP_Text _registerEmailTip;
        private readonly TMP_Text _registerPasswordTip;
        private readonly TMP_Text _confirmPasswordTip;
        private readonly TMP_Text _statusText;

        public AuthFormBindings(
            GameObject loginPanel,
            GameObject registerPanel,
            TMP_InputField loginEmailInput,
            TMP_InputField loginPasswordInput,
            TMP_InputField registerEmailInput,
            TMP_InputField registerPasswordInput,
            TMP_InputField confirmPasswordInput,
            Button loginButton,
            Button goRegisterButton,
            Button registerButton,
            Button goLoginButton,
            TMP_Text loginEmailTip,
            TMP_Text loginPasswordTip,
            TMP_Text registerEmailTip,
            TMP_Text registerPasswordTip,
            TMP_Text confirmPasswordTip,
            TMP_Text statusText)
        {
            _loginPanel = loginPanel;
            _registerPanel = registerPanel;
            _loginEmailInput = loginEmailInput;
            _loginPasswordInput = loginPasswordInput;
            _registerEmailInput = registerEmailInput;
            _registerPasswordInput = registerPasswordInput;
            _confirmPasswordInput = confirmPasswordInput;
            _loginButton = loginButton;
            _goRegisterButton = goRegisterButton;
            _registerButton = registerButton;
            _goLoginButton = goLoginButton;
            _loginEmailTip = loginEmailTip;
            _loginPasswordTip = loginPasswordTip;
            _registerEmailTip = registerEmailTip;
            _registerPasswordTip = registerPasswordTip;
            _confirmPasswordTip = confirmPasswordTip;
            _statusText = statusText;
        }

        public LoginRequest CreateLoginRequest()
        {
            return new LoginRequest
            {
                Email = ReadInput(_loginEmailInput),
                Password = ReadInput(_loginPasswordInput)
            };
        }

        public RegisterRequest CreateRegisterRequest()
        {
            return new RegisterRequest
            {
                Email = ReadInput(_registerEmailInput).Trim(),
                Password = ReadInput(_registerPasswordInput),
                ConfirmPassword = ReadInput(_confirmPasswordInput)
            };
        }

        public string RegisterEmail => ReadInput(_registerEmailInput).Trim();

        public void ShowLoginPanel()
        {
            ClearRegisterInputs();
            HideRegisterInputTips();
            SetPanelActive(_loginPanel, true);
            SetPanelActive(_registerPanel, false);
            SetStatus(string.Empty);
        }

        public void ShowRegisterPanel()
        {
            ClearLoginInputs();
            HideLoginInputTips();
            SetPanelActive(_loginPanel, false);
            SetPanelActive(_registerPanel, true);
            SetStatus(string.Empty);
        }

        public void SetLoginEmail(string email)
        {
            if (_loginEmailInput != null)
            {
                _loginEmailInput.text = email ?? string.Empty;
            }
        }

        public void ShowResult(AuthResult result, bool isLogin)
        {
            if (result == null)
            {
                return;
            }

            if (result.Success)
            {
                if (isLogin)
                {
                    HideLoginInputTips();
                }
                else
                {
                    HideRegisterInputTips();
                }

                SetStatus(result.Message);
                return;
            }

            SetStatus(string.Empty);
            ShowFieldTip(result.Field, result.Message, isLogin);
        }

        public void HideAllInputTips()
        {
            HideLoginInputTips();
            HideRegisterInputTips();
        }

        public void BindTipClearEvents()
        {
            BindInputSelect(_loginEmailInput, HideLoginEmailTip);
            BindInputSelect(_loginPasswordInput, HideLoginPasswordTip);
            BindInputSelect(_registerEmailInput, HideRegisterEmailTip);
            BindInputSelect(_registerPasswordInput, HideRegisterPasswordTip);
            BindInputSelect(_confirmPasswordInput, HideConfirmPasswordTip);
        }

        public void UnbindTipClearEvents()
        {
            UnbindInputSelect(_loginEmailInput, HideLoginEmailTip);
            UnbindInputSelect(_loginPasswordInput, HideLoginPasswordTip);
            UnbindInputSelect(_registerEmailInput, HideRegisterEmailTip);
            UnbindInputSelect(_registerPasswordInput, HideRegisterPasswordTip);
            UnbindInputSelect(_confirmPasswordInput, HideConfirmPasswordTip);
        }

        public void SetInteractable(bool interactable)
        {
            SetButtonInteractable(_loginButton, interactable);
            SetButtonInteractable(_goRegisterButton, interactable);
            SetButtonInteractable(_registerButton, interactable);
            SetButtonInteractable(_goLoginButton, interactable);
        }

        public void ConfigurePasswordFields()
        {
            ConfigurePasswordField(_loginPasswordInput);
            ConfigurePasswordField(_registerPasswordInput);
            ConfigurePasswordField(_confirmPasswordInput);
        }

        public void SetStatus(string message)
        {
            if (_statusText == null)
            {
                return;
            }

            string text = message ?? string.Empty;
            _statusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _statusText.text = text;
        }

        private void ClearLoginInputs()
        {
            ClearInput(_loginEmailInput);
            ClearInput(_loginPasswordInput);
        }

        private void ClearRegisterInputs()
        {
            ClearInput(_registerEmailInput);
            ClearInput(_registerPasswordInput);
            ClearInput(_confirmPasswordInput);
        }

        private void ShowFieldTip(AuthField field, string message, bool isLogin)
        {
            switch (field)
            {
                case AuthField.Email:
                    SetInputTip(isLogin ? _loginEmailTip : _registerEmailTip, message);
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

        private void HideLoginInputTips()
        {
            HideInputTip(_loginEmailTip);
            HideInputTip(_loginPasswordTip);
        }

        private void HideRegisterInputTips()
        {
            HideInputTip(_registerEmailTip);
            HideInputTip(_registerPasswordTip);
            HideInputTip(_confirmPasswordTip);
        }

        private void HideLoginEmailTip(string _) => HideInputTip(_loginEmailTip);
        private void HideLoginPasswordTip(string _) => HideInputTip(_loginPasswordTip);
        private void HideRegisterEmailTip(string _) => HideInputTip(_registerEmailTip);
        private void HideRegisterPasswordTip(string _) => HideInputTip(_registerPasswordTip);
        private void HideConfirmPasswordTip(string _) => HideInputTip(_confirmPasswordTip);

        private static string ReadInput(TMP_InputField input)
        {
            return input == null ? string.Empty : input.text;
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private static void ClearInput(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            input.text = string.Empty;
            input.DeactivateInputField();
        }

        private static void ConfigurePasswordField(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            input.contentType = TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();
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

        private static void BindInputSelect(TMP_InputField input, UnityAction<string> callback)
        {
            if (input != null)
            {
                input.onSelect.AddListener(callback);
            }
        }

        private static void UnbindInputSelect(TMP_InputField input, UnityAction<string> callback)
        {
            if (input != null)
            {
                input.onSelect.RemoveListener(callback);
            }
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
```

- [ ] **Step 2: Preserve current hierarchy lookup in one clearly marked View method.**

Keep `AutoBindIfNeeded`, `FindInput`, `FindTip`, `FindButton`, `FindDeepChild`, and `FindDirectChild` in `AuthUIView`. Add this Chinese rationale before `AutoBindIfNeeded`:

```csharp
// 当前 LoginMenu 依赖运行时路径绑定以兼容已有场景；本次只隔离该技术债，不重命名节点或修改序列化引用。
```

Create `_formBindings` only after `AutoBindIfNeeded()` completes in `Awake`, passing the exact current controls:

```csharp
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
```

`AuthUIView` must continue to use the same `Button.onClick` listeners.

- [ ] **Step 3: Reduce `AuthUIView` to lifecycle and event adaptation.**

Replace input object construction with:

```csharp
AuthResult result = await _authController.LoginAsync(_formBindings.CreateLoginRequest());
```

and:

```csharp
AuthResult result = await _authController.RegisterAsync(_formBindings.CreateRegisterRequest());
```

Replace the panel methods with:

```csharp
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
```

Replace direct presentation calls with `_formBindings.ShowResult(result, isLogin: true)` and `_formBindings.ShowResult(result, isLogin: false)`. For successful registration, preserve the existing post-result order exactly:

```csharp
ShowLogin();
_formBindings.SetLoginEmail(registeredEmail);
_formBindings.SetStatus(result.Message);
```

Replace `OnEnable` and `OnDisable` input-event calls with `_formBindings.BindTipClearEvents()` and `_formBindings.UnbindTipClearEvents()`. Keep `async void` only on `Start`, `OnLoginClicked`, and `OnRegisterClicked`, because they are Unity message or button callback entry points; add a Chinese comment at each boundary explaining this constraint.

- [ ] **Step 4: Compile `AuthUIView` and inspect its live component bindings.**

Run through Unity MCP:

1. `unity_script.validate` for `Assets/FireBase+Photon/Scripts/Auth/AuthFormBindings.cs`.
2. `unity_script.validate` for `Assets/FireBase+Photon/Scripts/Auth/AuthUIView.cs`.
3. `unity_component.info` for `Canvas`, component type `AuthUIView`.

Expected: no compile diagnostics; the existing `Canvas` component still resolves `LoginPanel` and `StatusText`, while remaining references can continue to be runtime-bound.

- [ ] **Step 5: Commit the UI-only responsibility extraction.**

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/Auth/AuthFormBindings.cs' 'Assets/FireBase+Photon/Scripts/Auth/AuthUIView.cs'
git commit -m "refactor: isolate auth form presentation"
```

### Task 4: Extract The Successful-Login Continuation

**Files:**

- Create: `Assets/FireBase+Photon/Scripts/Auth/AuthLoginFlowCoordinator.cs`
- Modify: `Assets/FireBase+Photon/Scripts/Auth/AuthUIView.cs:235-270`

- [ ] **Step 1: Add the coordinator with a narrow continuation contract.**

```csharp
using System;
using System.Threading.Tasks;
using ARPG.GameFlow;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// 认证成功后的续流程协调器。
    /// 只负责“昵称档案就绪后进入 Play”，不处理表单输入、Firebase 登录或场景内网络对象。
    /// </summary>
    internal sealed class AuthLoginFlowCoordinator
    {
        private readonly UsernamePanelView _usernamePanelView;
        private readonly LoadingOverlayView _loadingOverlay;
        private readonly Action<bool> _setSubmitting;
        private readonly Action<string> _setStatus;

        public AuthLoginFlowCoordinator(
            UsernamePanelView usernamePanelView,
            LoadingOverlayView loadingOverlay,
            Action<bool> setSubmitting,
            Action<string> setStatus)
        {
            _usernamePanelView = usernamePanelView;
            _loadingOverlay = loadingOverlay;
            _setSubmitting = setSubmitting;
            _setStatus = setStatus;
        }

        public void ContinueAfterLogin()
        {
            _setSubmitting(true);
            _usernamePanelView.CheckCurrentPlayerNameAsync(EnterPlaySceneAsync, OnProfileFlowAborted);
        }

        private void OnProfileFlowAborted()
        {
            _setSubmitting(false);
        }

        // UsernamePanelView 当前以 Action 回调通知昵称就绪；适配层只能使用 void，实际逻辑仍保留为 Task。
        private async void EnterPlaySceneAsync()
        {
            await EnterPlaySceneInternalAsync();
        }

        private async Task EnterPlaySceneInternalAsync()
        {
            _loadingOverlay.Show();
            try
            {
                await GameSceneController.LoadPlaySceneAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _loadingOverlay.Hide();
                _setSubmitting(false);
                _setStatus("Failed to enter the game scene.");
            }
        }
    }
}
```

- [ ] **Step 2: Compose the coordinator once in `AuthUIView.Awake`.**

After resolving `_usernamePanelView` and `_loadingOverlay`, create:

```csharp
_loginFlowCoordinator = new AuthLoginFlowCoordinator(
    _usernamePanelView,
    _loadingOverlay,
    SetSubmitting,
    SetStatus);
```

Add a private readonly-or-assigned field `AuthLoginFlowCoordinator _loginFlowCoordinator`. Do not make it static.

- [ ] **Step 3: Replace the success continuation and delete `EnterPlayScene` from `AuthUIView`.**

Replace the existing success block after `ShowLoginResult(result)` with:

```csharp
if (result.Success)
{
    Debug.Log("[Login] Auth success, start profile check before Play.");
    _loginFlowCoordinator.ContinueAfterLogin();
}
```

Delete `AuthUIView.EnterPlayScene`. The coordinator must retain the exact existing status message and restore the submit state on profile abort or scene-load failure.

- [ ] **Step 4: Compile and run focused static checks.**

Run through Unity MCP: `unity_script.validate` for `AuthLoginFlowCoordinator.cs` and `AuthUIView.cs`, then `unity_script.find` with pattern `EnterPlayScene` under `Assets/FireBase+Photon/Scripts/Auth`.

Expected: only `EnterPlaySceneAsync` and `EnterPlaySceneInternalAsync` remain in `AuthLoginFlowCoordinator`; no `AuthUIView.EnterPlayScene` remains.

- [ ] **Step 5: Commit the continuation extraction.**

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/Auth/AuthLoginFlowCoordinator.cs' 'Assets/FireBase+Photon/Scripts/Auth/AuthUIView.cs'
git commit -m "refactor: isolate auth login continuation"
```

### Task 5: Document Existing Boundaries And Verify Unity Integration

**Files:**

- Modify: `Assets/FireBase+Photon/Scripts/Auth/FirebaseAuthManager.cs:9-12,49-51,143-144`
- Modify: `Assets/FireBase+Photon/Scripts/Auth/AuthSessionGuard.cs:11-13,56-58,186`
- Modify: `Assets/FireBase+Photon/Scripts/Auth/FusionAuthSessionBridge.cs:9-11,44-54,56-72`
- Modify: `Docs/Superpowers/WorkStatus.md`

- [ ] **Step 1: Add Chinese comments that explain existing non-obvious constraints without changing behavior.**

Use these comments at the relevant boundaries:

```csharp
// 初始化任务被缓存以避免重复依赖检查；失败重试策略属于后续可靠性改造，不在本次低风险重构中改变。
```

```csharp
// 当前断线策略会释放 Firestore 租约；是否自动重连属于网络状态机改造，本次只记录边界。
```

```csharp
// INetworkRunnerCallbacks 要求实现完整接口；空回调表示该事件不影响当前会话租约生命周期。
```

Do not write comments that claim Firebase-to-Photon server authentication exists.

- [ ] **Step 2: Run project compilation through Unity MCP.**

Run `unity_editor.compile` and wait until `unity_editor.editor_info` reports `isCompiling: false`.

Expected: the editor returns to idle without project-script compilation errors.

- [ ] **Step 3: Validate the affected scene and inspect errors.**

Run through Unity MCP:

1. `unity_scene.open` with `Assets/FireBase+Photon/Scenes/LoginMenu.unity`.
2. `unity_validation.validate_scene` with `checkMissingScripts: true` and `checkDuplicateNames: true`.
3. `unity_editor.get_logs` with `types: ["error"]`.

Expected: no missing scripts and no new project error logs. Existing TMP duplicate-name warnings are recorded as non-blocking and must not trigger hierarchy renames in this scope.

- [ ] **Step 4: Run all EditMode tests and update the status table.**

Run `unity_test.run` with `mode: "EditMode"`, wait for `unity_test.status`, and record the result in `Docs/Superpowers/WorkStatus.md`. Change implementation, tests, comments, and Unity MCP verification rows to `已完成` only when their checks pass.

- [ ] **Step 5: Commit the documentation and verification result.**

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/Auth/FirebaseAuthManager.cs' 'Assets/FireBase+Photon/Scripts/Auth/AuthSessionGuard.cs' 'Assets/FireBase+Photon/Scripts/Auth/FusionAuthSessionBridge.cs' 'Docs/Superpowers/WorkStatus.md'
git commit -m "docs: clarify auth lifecycle boundaries"
```

## Plan Self-Review

- Scope coverage: Tasks 1-4 implement the agreed code-structure, readability, Chinese-comment, and duplicate-state work. Task 5 supplies Unity MCP and test evidence.
- Compatibility: no task changes Firebase fields, Firestore session behavior, Fusion startup, scene node names, or serialized component references.
- Testability: request validation is extracted into a pure class before tests are added; tests avoid real Firebase and Firestore access.
- Consistency: `UserSession` is the sole stated runtime session source in every task; `AuthLoginFlowCoordinator` is the only new owner of profile-ready to scene-entry continuation.
