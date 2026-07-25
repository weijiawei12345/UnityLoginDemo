using TMPro;

namespace ARPG.Auth
{
    /// <summary>
    /// 将登录界面的 TMP 输入框转换为认证请求对象。
    /// View 只负责传入控件引用，认证用例不需要了解 Unity UI 类型。
    /// </summary>
    public static class AuthFormRequestFactory
    {
        public static LoginRequest CreateLoginRequest(
            TMP_InputField emailInput,
            TMP_InputField passwordInput)
        {
            return new LoginRequest
            {
                Email = ReadText(emailInput),
                Password = ReadText(passwordInput)
            };
        }

        public static RegisterRequest CreateRegisterRequest(
            TMP_InputField emailInput,
            TMP_InputField passwordInput,
            TMP_InputField confirmPasswordInput)
        {
            return new RegisterRequest
            {
                Email = ReadText(emailInput).Trim(),
                Password = ReadText(passwordInput),
                ConfirmPassword = ReadText(confirmPasswordInput)
            };
        }

        private static string ReadText(TMP_InputField input)
        {
            return input == null ? string.Empty : input.text;
        }
    }
}
