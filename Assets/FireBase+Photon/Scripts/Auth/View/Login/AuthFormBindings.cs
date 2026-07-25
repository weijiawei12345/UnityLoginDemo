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

        public string RegisterEmail => ReadInput(_registerEmailInput).Trim();

        public LoginRequest CreateLoginRequest()
        {
            return AuthFormRequestFactory.CreateLoginRequest(
                _loginEmailInput,
                _loginPasswordInput);
        }

        public RegisterRequest CreateRegisterRequest()
        {
            return AuthFormRequestFactory.CreateRegisterRequest(
                _registerEmailInput,
                _registerPasswordInput,
                _confirmPasswordInput);
        }

        public void ConfigurePasswordFields()
        {
            ConfigurePasswordField(_loginPasswordInput);
            ConfigurePasswordField(_registerPasswordInput);
            ConfigurePasswordField(_confirmPasswordInput);
        }

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

        public void HideLoginInputTips()
        {
            HideInputTip(_loginEmailTip);
            HideInputTip(_loginPasswordTip);
        }

        public void HideRegisterInputTips()
        {
            HideInputTip(_registerEmailTip);
            HideInputTip(_registerPasswordTip);
            HideInputTip(_confirmPasswordTip);
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
