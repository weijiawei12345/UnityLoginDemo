using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARPG.GameFlow;
using Firebase.Firestore;
using Fusion;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// 登录成功后挂载：心跳 + 会话监听；被顶号时踢下线回登录页。
    /// </summary>
    public sealed class AuthSessionGuard : MonoBehaviour
    {
        private const float HeartbeatIntervalSeconds = 3f;

        private static AuthSessionGuard _instance;

        private readonly FirestoreAuthSessionRepository _repository = new FirestoreAuthSessionRepository();
        private string _uid;
        private string _sessionId;
        private Coroutine _heartbeatRoutine;
        private Coroutine _runnerWatchRoutine;
        private ListenerRegistration _sessionListener;
        private bool _releasing;
        private bool _handlingKick;
        private bool _kickRequested;

        public static bool IsActive =>
            _instance != null
            && !string.IsNullOrEmpty(_instance._uid)
            && !string.IsNullOrEmpty(_instance._sessionId);

        public static void Begin(string uid, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            AuthSessionGuard guard = EnsureInstance();
            guard.StopRoutines();
            guard.StopListener();
            guard._uid = uid;
            guard._sessionId = sessionId;
            guard._releasing = false;
            guard._handlingKick = false;
            guard._kickRequested = false;
            guard.StartListener(uid, sessionId);
            guard._heartbeatRoutine = guard.StartCoroutine(guard.HeartbeatLoop());
            guard._runnerWatchRoutine = guard.StartCoroutine(guard.WatchNetworkRunners());
            Debug.Log($"[AuthSession] Guard started. uid={uid}");
        }

        /// <summary>
        /// Fusion 离开房间 / 断线时调用，立即写 Firestore 离线（仅本端仍持有会话时）。
        /// </summary>
        public static void NotifyPhotonOffline()
        {
            if (_instance == null || _instance._handlingKick)
            {
                return;
            }

            _ = _instance.ReleaseInternalAsync();
        }

        public static async Task EndAsync()
        {
            if (_instance == null)
            {
                return;
            }

            await _instance.ReleaseInternalAsync();
        }

        private static AuthSessionGuard EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject go = new GameObject(nameof(AuthSessionGuard));
            _instance = go.AddComponent<AuthSessionGuard>();
            DontDestroyOnLoad(go);
            return _instance;
        }

        private void StartListener(string uid, string sessionId)
        {
            _sessionListener = _repository.ListenSession(uid, activeSessionId =>
            {
                if (string.IsNullOrEmpty(_sessionId))
                {
                    return;
                }

                if (!string.Equals(activeSessionId, sessionId, System.StringComparison.Ordinal))
                {
                    _kickRequested = true;
                }
            });
        }

        private void Update()
        {
            if (_kickRequested && !_handlingKick)
            {
                _kickRequested = false;
                _ = HandleKickedAsync();
            }
        }

        private IEnumerator HeartbeatLoop()
        {
            var wait = new WaitForSecondsRealtime(HeartbeatIntervalSeconds);
            while (!string.IsNullOrEmpty(_uid) && !string.IsNullOrEmpty(_sessionId))
            {
                string uid = _uid;
                string sessionId = _sessionId;
                Task<AuthHeartbeatResult> heartbeatTask = _repository.HeartbeatAsync(uid, sessionId);
                while (!heartbeatTask.IsCompleted)
                {
                    yield return null;
                }

                if (heartbeatTask.Result == AuthHeartbeatResult.Replaced)
                {
                    _kickRequested = true;
                    yield break;
                }

                yield return wait;
            }
        }

        private IEnumerator WatchNetworkRunners()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (!string.IsNullOrEmpty(_uid) && !string.IsNullOrEmpty(_sessionId))
            {
                AttachBridgeToRunners();
                yield return wait;
            }

            _runnerWatchRoutine = null;
        }

        private static void AttachBridgeToRunners()
        {
            var runners = NetworkRunner.Instances;
            if (runners == null)
            {
                return;
            }

            for (int i = 0; i < runners.Count; i++)
            {
                NetworkRunner runner = runners[i];
                if (runner == null)
                {
                    continue;
                }

                if (runner.GetComponent<FusionAuthSessionBridge>() == null)
                {
                    runner.gameObject.AddComponent<FusionAuthSessionBridge>();
                    Debug.Log($"[AuthSession] Attached Fusion bridge to runner '{runner.name}'.");
                }
            }
        }

        private async Task HandleKickedAsync()
        {
            if (_handlingKick)
            {
                return;
            }

            _handlingKick = true;
            Debug.LogWarning("[AuthSession] Kicked by remote login.");

            // 被顶号：不要 Release，避免清掉新端的 sessionId
            ClearLocal();
            await ShutdownAllRunnersAsync();

            FirebaseAuthManager.Instance.SignOut();
            UserSession.Clear();
            AuthKickNotice.Set(AuthKickNotice.DefaultMessage);
            GameSceneController.LoadLoginMenuScene();
        }

        private static async Task ShutdownAllRunnersAsync()
        {
            var runners = NetworkRunner.Instances;
            if (runners == null || runners.Count == 0)
            {
                return;
            }

            var copy = new List<NetworkRunner>(runners);
            for (int i = 0; i < copy.Count; i++)
            {
                NetworkRunner runner = copy[i];
                if (runner == null)
                {
                    continue;
                }

                try
                {
                    await runner.Shutdown();
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[AuthSession] Runner shutdown failed: {exception.Message}");
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (_handlingKick || string.IsNullOrEmpty(_uid) || string.IsNullOrEmpty(_sessionId) || _releasing)
            {
                return;
            }

            _releasing = true;
            string uid = _uid;
            string sessionId = _sessionId;
            ClearLocal();
            _ = _repository.ReleaseAsync(uid, sessionId);
        }

        private void OnDestroy()
        {
            StopListener();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private async Task ReleaseInternalAsync()
        {
            if (_handlingKick || _releasing || string.IsNullOrEmpty(_uid) || string.IsNullOrEmpty(_sessionId))
            {
                ClearLocal();
                return;
            }

            _releasing = true;
            string uid = _uid;
            string sessionId = _sessionId;
            ClearLocal();
            await _repository.ReleaseAsync(uid, sessionId);
        }

        private void ClearLocal()
        {
            StopRoutines();
            StopListener();
            _uid = null;
            _sessionId = null;
        }

        private void StopListener()
        {
            if (_sessionListener != null)
            {
                _sessionListener.Stop();
                _sessionListener = null;
            }
        }

        private void StopRoutines()
        {
            if (_heartbeatRoutine != null)
            {
                StopCoroutine(_heartbeatRoutine);
                _heartbeatRoutine = null;
            }

            if (_runnerWatchRoutine != null)
            {
                StopCoroutine(_runnerWatchRoutine);
                _runnerWatchRoutine = null;
            }
        }
    }
}
