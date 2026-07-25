using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// Firestore 单例与 Desktop Persistence 关闭配置（各 Repository 共用）。
    /// </summary>
    internal static class FirestoreClient
    {
        private static FirebaseFirestore _firestore;
        private static bool _firestoreConfigured;
        private static readonly object ConfigureLock = new object();

        public static async Task<bool> EnsureReadyAsync()
        {
            FirebaseInitializationResult initialization = await FirebaseAuthManager.Instance.InitializeAsync();
            return initialization.Success;
        }

        public static FirebaseFirestore Get()
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
                _firestore.Settings.PersistenceEnabled = false;
                _firestoreConfigured = true;
                Debug.Log("[Firestore] DefaultInstance ready. PersistenceEnabled=false");
            }

            return _firestore;
        }
    }
}
