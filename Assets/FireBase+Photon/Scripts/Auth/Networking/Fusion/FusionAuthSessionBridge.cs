using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace ARPG.Auth
{
    /// <summary>
    /// 挂到 NetworkRunner 上：离开 Photon Session / 断线时立即释放 Firestore 在线锁。
    /// </summary>
    public sealed class FusionAuthSessionBridge : MonoBehaviour, INetworkRunnerCallbacks
    {
        private bool _notified;

        private void Awake()
        {
            NetworkRunner runner = GetComponent<NetworkRunner>();
            if (runner != null)
            {
                runner.AddCallbacks(this);
            }
        }

        private void OnDestroy()
        {
            NetworkRunner runner = GetComponent<NetworkRunner>();
            if (runner != null)
            {
                runner.RemoveCallbacks(this);
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            NotifyOffline($"Shutdown:{shutdownReason}");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            NotifyOffline($"Disconnected:{reason}");
        }

        private void NotifyOffline(string reason)
        {
            if (_notified)
            {
                return;
            }

            _notified = true;
            Debug.Log($"[AuthSession] Photon offline signal: {reason}");
            AuthSessionGuard.NotifyPhotonOffline();
        }

        // INetworkRunnerCallbacks 要求实现完整接口；空回调表示该事件不影响当前会话租约生命周期。
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
