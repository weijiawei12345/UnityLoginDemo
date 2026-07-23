using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// Firebase Auth 的唯一入口。
    /// 负责 SDK 初始化、邮箱认证和统一错误映射，不处理 UI 与用户档案持久化。
    /// </summary>
    public sealed class FirebaseAuthManager : MonoBehaviour
    {
        private static FirebaseAuthManager _instance;

        private FirebaseAuth _auth;
        private Task<FirebaseInitializationResult> _initializationTask;

        public static FirebaseAuthManager Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                GameObject managerObject = new GameObject(nameof(FirebaseAuthManager));
                _instance = managerObject.AddComponent<FirebaseAuthManager>();
                return _instance;
            }
        }

        public bool IsReady { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 初始化只执行一次，后续认证请求复用同一个 Task，避免重复检查 Firebase 依赖。
        /// </summary>
        public Task<FirebaseInitializationResult> InitializeAsync()
        {
            if (_initializationTask == null)
            {
                _initializationTask = InitializeInternalAsync();
            }

            return _initializationTask;
        }

        public async Task<FirebaseAuthOperationResult> RegisterAsync(string email, string password)
        {
            FirebaseInitializationResult initialization = await InitializeAsync();
            if (!initialization.Success)
            {
                return FirebaseAuthOperationResult.Fail(initialization.Message);
            }

            return await ExecuteAuthTask(
                _auth.CreateUserWithEmailAndPasswordAsync(email, password),
                "Registration failed. Please try again.");
        }

        public async Task<FirebaseAuthOperationResult> LoginAsync(string email, string password)
        {
            FirebaseInitializationResult initialization = await InitializeAsync();
            if (!initialization.Success)
            {
                return FirebaseAuthOperationResult.Fail(initialization.Message);
            }

            return await ExecuteAuthTask(
                _auth.SignInWithEmailAndPasswordAsync(email, password),
                "Incorrect email or password.");
        }

        public void SignOut()
        {
            if (!IsReady)
            {
                return;
            }

            _auth.SignOut();
        }

        private async Task<FirebaseInitializationResult> InitializeInternalAsync()
        {
            try
            {
                DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (dependencyStatus != DependencyStatus.Available)
                {
                    Debug.LogError($"[FirebaseAuth] Dependency check failed: {dependencyStatus}");
                    return FirebaseInitializationResult.Fail("Firebase is not available. Please check the project configuration.");
                }

                _auth = FirebaseAuth.DefaultInstance;
                IsReady = true;
                Debug.Log("[FirebaseAuth] Firebase Auth initialized.");
                return FirebaseInitializationResult.Ok();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return FirebaseInitializationResult.Fail("Firebase initialization failed. Please try again later.");
            }
        }

        private static async Task<FirebaseAuthOperationResult> ExecuteAuthTask(
            Task<Firebase.Auth.AuthResult> authTask,
            string defaultErrorMessage)
        {
            try
            {
                Firebase.Auth.AuthResult result = await authTask;
                return FirebaseAuthOperationResult.Ok(result.User);
            }
            catch (FirebaseException exception)
            {
                Debug.LogWarning($"[FirebaseAuth] Authentication failed: {exception.ErrorCode}. {exception.Message}");
                return FirebaseAuthOperationResult.Fail(
                    GetFriendlyErrorMessage((AuthError)exception.ErrorCode, defaultErrorMessage));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return FirebaseAuthOperationResult.Fail(defaultErrorMessage);
            }
        }

        // Firebase 可能将错误密码包装为通用凭据异常，因此登录操作有专用兜底文案。
        private static string GetFriendlyErrorMessage(AuthError error, string defaultErrorMessage)
        {
            switch (error)
            {
                case AuthError.InvalidEmail:
                    return "Please enter a valid email address.";
                case AuthError.EmailAlreadyInUse:
                    return "This email is already in use.";
                case AuthError.WeakPassword:
                    return "Password must be at least 6 characters.";
                case AuthError.InvalidCredential:
                case AuthError.WrongPassword:
                case AuthError.UserNotFound:
                    return "Incorrect email or password.";
                case AuthError.NetworkRequestFailed:
                    return "Network error. Please check your connection.";
                case AuthError.TooManyRequests:
                    return "Too many attempts. Please try again later.";
                default:
                    return defaultErrorMessage;
            }
        }
    }

    public sealed class FirebaseInitializationResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        public static FirebaseInitializationResult Ok()
        {
            return new FirebaseInitializationResult { Success = true };
        }

        public static FirebaseInitializationResult Fail(string message)
        {
            return new FirebaseInitializationResult { Success = false, Message = message };
        }
    }

    public sealed class FirebaseAuthOperationResult
    {
        public bool Success { get; private set; }
        public FirebaseUser User { get; private set; }
        public string Message { get; private set; }

        public static FirebaseAuthOperationResult Ok(FirebaseUser user)
        {
            return new FirebaseAuthOperationResult { Success = true, User = user };
        }

        public static FirebaseAuthOperationResult Fail(string message)
        {
            return new FirebaseAuthOperationResult { Success = false, Message = message };
        }
    }
}
