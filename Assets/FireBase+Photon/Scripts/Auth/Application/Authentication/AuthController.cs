using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// Auth Controller：保留前端校验职责，并将 Firebase 的结果转换为业务数据。
    /// 登录成功后强制占用 Firestore 会话（顶号：踢掉旧端）。
    /// </summary>
    public class AuthController
    {
        private readonly FirestoreAuthSessionRepository _sessionRepository = new FirestoreAuthSessionRepository();

        public async Task<AuthResult> LoginAsync(LoginRequest request)
        {
            Debug.Log("[Login] Login button clicked");

            AuthResult validationResult = AuthRequestValidator.ValidateLogin(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            FirebaseAuthOperationResult firebaseResult = await FirebaseAuthManager.Instance.LoginAsync(
                request.Email.Trim(), request.Password);
            if (!firebaseResult.Success)
            {
                return AuthResult.Fail(firebaseResult.Message, AuthField.Email);
            }

            string sessionId = Guid.NewGuid().ToString("N");
            AuthSessionResult sessionResult = await _sessionRepository.ForceAcquireAsync(
                firebaseResult.User.UserId,
                sessionId);
            if (!sessionResult.Success)
            {
                FirebaseAuthManager.Instance.SignOut();
                Debug.LogWarning($"[Login] Session acquire failed: {sessionResult.Message}");
                return AuthResult.Fail(sessionResult.Message, AuthField.Email);
            }

            UserData user = CreateSessionUser(firebaseResult.User, request.Email, sessionId);
            AuthSessionGuard.Begin(user.Uid, sessionId);
            Debug.Log($"[Login] Login success. user={user.Name}, uid={user.Uid}");
            return AuthResult.Ok(user, "Login successful.");
        }

        public async Task<AuthResult> RegisterAsync(RegisterRequest request)
        {
            Debug.Log("[Register] Register button clicked");

            AuthResult validationResult = AuthRequestValidator.ValidateRegistration(request);
            if (validationResult != null)
            {
                return validationResult;
            }

            FirebaseAuthOperationResult firebaseResult = await FirebaseAuthManager.Instance.RegisterAsync(
                request.Email.Trim(), request.Password);
            if (!firebaseResult.Success)
            {
                return AuthResult.Fail(firebaseResult.Message, AuthField.Email);
            }

            // 注册成功后回到登录页，不占用在线会话。
            FirebaseAuthManager.Instance.SignOut();
            Debug.Log($"[Register] Register success. email={request.Email.Trim()}");
            return AuthResult.Ok(null, "Registration successful.");
        }

        public async Task SignOutAsync()
        {
            await AuthSessionGuard.EndAsync();
            FirebaseAuthManager.Instance.SignOut();
            UserSession.Clear();
            Debug.Log("[Auth] Sign out.");
        }

        public void SignOut()
        {
            _ = SignOutAsync();
        }

        private UserData CreateSessionUser(FirebaseUser firebaseUser, string fallbackEmail, string sessionId)
        {
            string email = string.IsNullOrWhiteSpace(firebaseUser.Email) ? fallbackEmail.Trim() : firebaseUser.Email;
            UserData user = UserData.CreateDefault(firebaseUser.UserId, email);
            UserSession.SetUser(user, sessionId);
            return user;
        }
    }
}
