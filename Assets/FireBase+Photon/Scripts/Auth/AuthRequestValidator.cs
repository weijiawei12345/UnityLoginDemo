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
