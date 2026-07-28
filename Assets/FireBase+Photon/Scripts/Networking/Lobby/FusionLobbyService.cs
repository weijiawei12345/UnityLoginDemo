using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace ARPG.Networking.Lobby
{
    /// <summary>
    /// Consumes Fusion lobby callbacks and exposes immutable room snapshots to the UI.
    /// </summary>
    public sealed class FusionLobbyService : MonoBehaviour, INetworkRunnerCallbacks
    {
        private readonly List<LobbyRoomSnapshot> _rooms = new List<LobbyRoomSnapshot>();
        private NetworkRunner _runner;

        public event Action<IReadOnlyList<LobbyRoomSnapshot>> RoomsChanged;
        public event Action<string> ConnectionLost;
        public event Action<NetworkRunner> SceneLoadCompleted;

        public IReadOnlyList<LobbyRoomSnapshot> Rooms => _rooms;

        private void Awake()
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner != null)
            {
                _runner.AddCallbacks(this);
            }
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
            }
        }

        public void PublishCurrentRooms()
        {
            RoomsChanged?.Invoke(_rooms);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _rooms.Clear();
            if (sessionList != null)
            {
                for (int i = 0; i < sessionList.Count; i++)
                {
                    SessionInfo session = sessionList[i];
                    if (session == null || !session.IsValid || !session.IsVisible)
                    {
                        continue;
                    }

                    _rooms.Add(new LobbyRoomSnapshot(
                        session.Name,
                        session.PlayerCount,
                        session.MaxPlayers,
                        session.IsOpen,
                        session.IsVisible,
                        ReadStringProperty(session, "map", "play"),
                        ReadStringProperty(session, "difficulty", "normal"),
                        ReadStringProperty(session, "phase", "waiting"),
                        ReadStringProperty(session, "build", string.Empty)));
                }
            }

            _rooms.Sort((left, right) => string.Compare(
                left.Name,
                right.Name,
                StringComparison.OrdinalIgnoreCase));
            RoomsChanged?.Invoke(_rooms);
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            ConnectionLost?.Invoke($"Disconnected: {reason}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _rooms.Clear();
            RoomsChanged?.Invoke(_rooms);
            if (shutdownReason != ShutdownReason.Ok)
            {
                ConnectionLost?.Invoke($"Network stopped: {shutdownReason}");
            }
        }

        private static string ReadStringProperty(SessionInfo session, string key, string fallback)
        {
            if (session.Properties != null
                && session.Properties.TryGetValue(key, out SessionProperty property)
                && property.IsString)
            {
                return (string)property;
            }

            return fallback;
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            SceneLoadCompleted?.Invoke(runner);
        }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
