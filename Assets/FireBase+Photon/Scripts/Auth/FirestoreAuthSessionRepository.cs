using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// 基于 Firestore 的单点登录会话（顶号策略）。
    /// 新端登录直接覆盖 activeSessionId；旧端通过监听/心跳发现后强制下线。
    /// </summary>
    public sealed class FirestoreAuthSessionRepository
    {
        private const string UsersCollection = "Users";
        private const string SessionIdField = "activeSessionId";
        private const string SessionOnlineField = "sessionOnline";
        private const string SessionHeartbeatField = "sessionHeartbeatUnix";

        public async Task<AuthSessionResult> ForceAcquireAsync(string uid, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(sessionId))
            {
                return AuthSessionResult.Fail("The player session is invalid.");
            }

            if (!await FirestoreClient.EnsureReadyAsync())
            {
                return AuthSessionResult.Fail("Firebase is not available. Please try again later.");
            }

            try
            {
                DocumentReference document = GetUserDocument(uid);
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                Dictionary<string, object> fields = new Dictionary<string, object>
                {
                    { SessionIdField, sessionId },
                    { SessionOnlineField, true },
                    { SessionHeartbeatField, now }
                };
                await document.SetAsync(fields, SetOptions.MergeAll);

                Debug.Log($"[AuthSession] Force acquired (kick previous). uid={uid}, session={sessionId}");
                return AuthSessionResult.Ok(sessionId);
            }
            catch (FirebaseException exception)
            {
                Debug.LogWarning($"[AuthSession] Acquire failed: {exception.ErrorCode}. {exception.Message}");
                return AuthSessionResult.Fail("Unable to verify login session. Please try again.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return AuthSessionResult.Fail("Unable to verify login session. Please try again.");
            }
        }

        /// <summary>
        /// 刷新心跳。若 activeSessionId 已不是本端，返回 Replaced（被顶号）。
        /// </summary>
        public async Task<AuthHeartbeatResult> HeartbeatAsync(string uid, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(sessionId))
            {
                return AuthHeartbeatResult.Failed;
            }

            if (!await FirestoreClient.EnsureReadyAsync())
            {
                return AuthHeartbeatResult.Failed;
            }

            try
            {
                DocumentReference document = GetUserDocument(uid);
                AuthHeartbeatResult result = AuthHeartbeatResult.Ok;

                await FirestoreClient.Get().RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(document);
                    if (!snapshot.Exists
                        || !snapshot.TryGetValue(SessionIdField, out string activeId)
                        || string.IsNullOrWhiteSpace(activeId))
                    {
                        result = AuthHeartbeatResult.Replaced;
                        return;
                    }

                    if (!string.Equals(activeId, sessionId, StringComparison.Ordinal))
                    {
                        result = AuthHeartbeatResult.Replaced;
                        return;
                    }

                    Dictionary<string, object> fields = new Dictionary<string, object>
                    {
                        { SessionOnlineField, true },
                        { SessionHeartbeatField, DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                    };
                    transaction.Set(document, fields, SetOptions.MergeAll);
                    result = AuthHeartbeatResult.Ok;
                });

                return result;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AuthSession] Heartbeat failed: {exception.Message}");
                return AuthHeartbeatResult.Failed;
            }
        }

        public ListenerRegistration ListenSession(
            string uid,
            Action<string> onActiveSessionIdChanged)
        {
            if (string.IsNullOrWhiteSpace(uid) || onActiveSessionIdChanged == null)
            {
                return null;
            }

            DocumentReference document = GetUserDocument(uid);
            return document.Listen(snapshot =>
            {
                string activeId = string.Empty;
                if (snapshot != null
                    && snapshot.Exists
                    && snapshot.TryGetValue(SessionIdField, out string id)
                    && !string.IsNullOrWhiteSpace(id))
                {
                    activeId = id;
                }

                onActiveSessionIdChanged(activeId);
            });
        }

        public async Task ReleaseAsync(string uid, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            if (!await FirestoreClient.EnsureReadyAsync())
            {
                return;
            }

            try
            {
                DocumentReference document = GetUserDocument(uid);
                await FirestoreClient.Get().RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(document);
                    if (!snapshot.Exists
                        || !snapshot.TryGetValue(SessionIdField, out string activeId)
                        || !string.Equals(activeId, sessionId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    Dictionary<string, object> fields = new Dictionary<string, object>
                    {
                        { SessionOnlineField, false },
                        { SessionIdField, string.Empty },
                        { SessionHeartbeatField, 0L }
                    };
                    transaction.Set(document, fields, SetOptions.MergeAll);
                });

                Debug.Log($"[AuthSession] Released. uid={uid}, session={sessionId}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[AuthSession] Release failed: {exception.Message}");
            }
        }

        private static DocumentReference GetUserDocument(string uid)
        {
            return FirestoreClient.Get().Collection(UsersCollection).Document(uid);
        }
    }

    public enum AuthHeartbeatResult
    {
        Ok = 0,
        Replaced = 1,
        Failed = 2
    }

    public sealed class AuthSessionResult
    {
        public bool Success { get; private set; }
        public string SessionId { get; private set; }
        public string Message { get; private set; }

        public static AuthSessionResult Ok(string sessionId)
        {
            return new AuthSessionResult { Success = true, SessionId = sessionId };
        }

        public static AuthSessionResult Fail(string message)
        {
            return new AuthSessionResult { Success = false, Message = message };
        }
    }

    /// <summary>
    /// 被顶号后回到登录页时展示的提示。
    /// </summary>
    public static class AuthKickNotice
    {
        public const string DefaultMessage = "Logged in elsewhere.";

        public static string PendingMessage { get; private set; }

        public static void Set(string message)
        {
            PendingMessage = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;
        }

        public static string Consume()
        {
            string message = PendingMessage;
            PendingMessage = null;
            return message;
        }
    }
}
