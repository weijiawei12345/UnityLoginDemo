using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// Firestore 用户档案的数据访问层。
    /// 当前阶段只读写 Users/{uid} 文档的 name 字段。
    /// </summary>
    public sealed class FirestoreUserProfileRepository
    {
        private const string UsersCollection = "Users";
        private const string NameField = "name";

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
                DocumentSnapshot snapshot = await GetUserDocument(uid).GetSnapshotAsync();
                if (!snapshot.Exists || !snapshot.TryGetValue(NameField, out string name)
                    || string.IsNullOrWhiteSpace(name))
                {
                    return UserNameResult.Missing();
                }

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
                Dictionary<string, object> fields = new Dictionary<string, object>
                {
                    { NameField, playerName }
                };

                // MergeAll 不覆盖后续阶段增加的等级、金币等档案字段。
                await GetUserDocument(uid).SetAsync(fields, SetOptions.MergeAll);
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
            return FirebaseFirestore.DefaultInstance.Collection(UsersCollection).Document(uid);
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
