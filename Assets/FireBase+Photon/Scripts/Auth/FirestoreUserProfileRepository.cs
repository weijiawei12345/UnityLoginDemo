using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// Firestore 用户档案数据访问层（只读写 Users/{uid}.name）。
    ///
    /// Windows Standalone 注意：
    /// Cloud Firestore Desktop 仍是 beta。首次访问 DefaultInstance / 开启本地 Persistence
    /// 时，可能在原生层直接进程退出（无 C# Exception，Player.log 会突然截断）。
    /// 因此必须在任何读写前关闭 Persistence，并在托管层做失败降级。
    /// </summary>
    public sealed class FirestoreUserProfileRepository
    {
        private const string UsersCollection = "Users";
        private const string NameField = "name";

        private static FirebaseFirestore _firestore;
        private static bool _firestoreConfigured;
        private static readonly object ConfigureLock = new object();

        public async Task<UserNameResult> LoadNameAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return UserNameResult.Fail("The player session is invalid.");
            }

            if (!await EnsureFirebaseReadyAsync())
            {
                return UserNameResult.Fail("Firebase is not available. Please try again later.");
            }

            try
            {
                Debug.Log($"[Firestore] LoadName begin. uid={uid}");
                DocumentReference document = GetUserDocument(uid);
                Debug.Log("[Firestore] LoadName GetSnapshotAsync...");
                DocumentSnapshot snapshot = await document.GetSnapshotAsync();

                if (!snapshot.Exists || !snapshot.TryGetValue(NameField, out string name)
                    || string.IsNullOrWhiteSpace(name))
                {
                    Debug.Log("[Firestore] LoadName result: missing");
                    return UserNameResult.Missing();
                }

                Debug.Log($"[Firestore] LoadName result: found name={name.Trim()}");
                return UserNameResult.Found(name.Trim());
            }
            catch (FirebaseException exception)
            {
                Debug.LogWarning($"[Firestore] Failed to load player name: {exception.ErrorCode}. {exception.Message}");
                return UserNameResult.Fail("Unable to load player name. Please try again.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return UserNameResult.Fail("Unable to load player name. Please try again.");
            }
        }

        public async Task<UserNameResult> SaveNameAsync(string uid, string playerName)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return UserNameResult.Fail("The player session is invalid.");
            }

            if (!await EnsureFirebaseReadyAsync())
            {
                return UserNameResult.Fail("Firebase is not available. Please try again later.");
            }

            try
            {
                Debug.Log($"[Firestore] SaveName begin. uid={uid}, name={playerName}");
                Dictionary<string, object> fields = new Dictionary<string, object>
                {
                    { NameField, playerName }
                };

                // MergeAll 不覆盖后续阶段增加的等级、金币等档案字段。
                await GetUserDocument(uid).SetAsync(fields, SetOptions.MergeAll);
                Debug.Log("[Firestore] SaveName success.");
                return UserNameResult.Found(playerName);
            }
            catch (FirebaseException exception)
            {
                Debug.LogWarning($"[Firestore] Failed to save player name: {exception.ErrorCode}. {exception.Message}");
                return UserNameResult.Fail("Unable to save player name. Please try again.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return UserNameResult.Fail("Unable to save player name. Please try again.");
            }
        }

        private static async Task<bool> EnsureFirebaseReadyAsync()
        {
            FirebaseInitializationResult initialization = await FirebaseAuthManager.Instance.InitializeAsync();
            return initialization.Success;
        }

        private static DocumentReference GetUserDocument(string uid)
        {
            return GetFirestore().Collection(UsersCollection).Document(uid);
        }

        /// <summary>
        /// 获取并配置 Firestore 单例。
        /// PersistenceEnabled 必须在任何读写之前设为 false，否则 Windows 包上可能原生闪退。
        /// </summary>
        private static FirebaseFirestore GetFirestore()
        {
            if (_firestoreConfigured && _firestore != null)
            {
                return _firestore;
            }

            lock (ConfigureLock)
            {
                if (_firestoreConfigured && _firestore != null)
                {
                    return _firestore;
                }

                Debug.Log("[Firestore] Acquiring FirebaseFirestore.DefaultInstance...");
                _firestore = FirebaseFirestore.DefaultInstance;

                // Desktop beta 关键稳定手段：关闭 LevelDB 本地持久化。
                // 必须在第一次 GetSnapshot/Set 之前设置。
                _firestore.Settings.PersistenceEnabled = false;
                _firestoreConfigured = true;
                Debug.Log("[Firestore] DefaultInstance ready. PersistenceEnabled=false");
            }

            return _firestore;
        }
    }

    public sealed class UserNameResult
    {
        public bool Success { get; private set; }
        public bool HasName { get; private set; }
        public string Name { get; private set; }
        public string Message { get; private set; }

        public static UserNameResult Found(string name)
        {
            return new UserNameResult { Success = true, HasName = true, Name = name };
        }

        public static UserNameResult Missing()
        {
            return new UserNameResult { Success = true, HasName = false };
        }

        public static UserNameResult Fail(string message)
        {
            return new UserNameResult { Success = false, Message = message };
        }
    }
}
