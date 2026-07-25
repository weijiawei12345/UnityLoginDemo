using System;

namespace ARPG.Auth
{
    /// <summary>
    /// Firestore Users 文档结构（阶段 4 持久化将直接序列化此模型）。
    /// </summary>
    [Serializable]
    public class UserData
    {
        public string Uid;
        public string Email;
        public string Name;
        public int Level = 1;
        public int Coins = 1000;
        public long CreatedAtUnix;
        public long LastLoginUnix;

        public static UserData CreateDefault(string uid, string email, string displayName = null)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new UserData
            {
                Uid = uid,
                Email = email,
                Name = string.IsNullOrEmpty(displayName) ? BuildDefaultName(email) : displayName,
                Level = 1,
                Coins = 1000,
                CreatedAtUnix = now,
                LastLoginUnix = now
            };
        }

        public void TouchLastLogin()
        {
            LastLoginUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static string BuildDefaultName(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return "Player";
            }

            int at = email.IndexOf('@');
            return at > 0 ? email.Substring(0, at) : email;
        }
    }

    [Serializable]
    public class LoginRequest
    {
        public string Email;
        public string Password;
    }

    [Serializable]
    public class RegisterRequest
    {
        public string Email;
        public string Password;
        public string ConfirmPassword;
    }

    /// <summary>
    /// 校验失败时对应的输入框，用于驱动对应 InputTip。
    /// </summary>
    public enum AuthField
    {
        None = 0,
        Email = 1,
        Password = 2,
        ConfirmPassword = 3
    }

    public class AuthResult
    {
        public bool Success;
        public string Message;
        public UserData User;
        public AuthField Field;

        public static AuthResult Fail(string message, AuthField field = AuthField.None)
        {
            return new AuthResult { Success = false, Message = message, Field = field };
        }

        public static AuthResult Ok(UserData user, string message)
        {
            return new AuthResult { Success = true, Message = message, User = user, Field = AuthField.None };
        }
    }

    /// <summary>
    /// 当前进程唯一的认证会话状态。
    /// Firebase 用户、Firestore 单点登录租约和网络展示名都从这里读取；
    /// 业务代码不得再维护第二份当前用户副本。
    /// </summary>
    public static class UserSession
    {
        public static UserData Current { get; private set; }
        public static string ActiveSessionId { get; private set; }
        public static bool IsLoggedIn => Current != null && !string.IsNullOrEmpty(Current.Uid);

        public static void SetUser(UserData user, string sessionId = null)
        {
            // 登录成功后一次性写入用户与租约 ID，避免调用方分别更新两个状态字段。
            Current = user;
            ActiveSessionId = sessionId;
        }

        public static void Clear()
        {
            Current = null;
            ActiveSessionId = null;
        }
    }
}
