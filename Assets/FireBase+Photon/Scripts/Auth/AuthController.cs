using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// Auth Controller（纯 C#）：输入校验与占位登录/注册逻辑。
    /// 阶段 3 将在此替换为 FirebaseAuthManager 异步调用。
    /// </summary>
    public class AuthController
    {
        public UserData CurrentUser { get; private set; }

        public AuthResult Login(LoginRequest request)
        {
            Debug.Log("[Login] Login button clicked");

            if (request == null || string.IsNullOrWhiteSpace(request.UserName))
            {
                Debug.LogWarning("[Login] Account is empty");
                return AuthResult.Fail("Please enter your account or email.", AuthField.Account);
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                Debug.LogWarning("[Login] Password is empty");
                return AuthResult.Fail("Please enter your password.", AuthField.Password);
            }

            if (request.Password.Length < 6)
            {
                Debug.LogWarning("[Login] Password too short");
                return AuthResult.Fail("Password must be at least 6 characters.", AuthField.Password);
            }

            string account = request.UserName.Trim();
            UserData user = UserData.CreateDefault($"local_{account.GetHashCode():X}", account);
            user.TouchLastLogin();

            CurrentUser = user;
            UserSession.SetUser(user);

            Debug.Log($"[Login] Login success (placeholder). user={user.Name}, uid={user.Uid}, level={user.Level}, coins={user.Coins}");
            return AuthResult.Ok(user, "Login successful.");
        }

        public AuthResult Register(RegisterRequest request)
        {
            Debug.Log("[Login] Register button clicked");

            if (request == null || string.IsNullOrWhiteSpace(request.UserName))
            {
                Debug.LogWarning("[Register] Account is empty");
                return AuthResult.Fail("Please enter your account or email.", AuthField.Account);
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                Debug.LogWarning("[Register] Password is empty");
                return AuthResult.Fail("Please enter your password.", AuthField.Password);
            }

            if (request.Password.Length < 6)
            {
                Debug.LogWarning("[Register] Password too short");
                return AuthResult.Fail("Password must be at least 6 characters.", AuthField.Password);
            }

            if (request.Password != request.ConfirmPassword)
            {
                Debug.LogWarning("[Register] Password mismatch");
                return AuthResult.Fail("Passwords do not match.", AuthField.ConfirmPassword);
            }

            string account = request.UserName.Trim();
            UserData user = UserData.CreateDefault($"local_{account.GetHashCode():X}", account);

            CurrentUser = user;
            UserSession.SetUser(user);

            Debug.Log($"[Register] Register success (placeholder). user={user.Name}, uid={user.Uid}");
            return AuthResult.Ok(user, "Registration successful. Please log in.");
        }

        public void SignOut()
        {
            CurrentUser = null;
            UserSession.Clear();
            Debug.Log("[Login] SignOut");
        }
    }
}
