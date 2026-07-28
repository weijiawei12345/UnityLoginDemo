using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARPG.Player.Input;
using ARPG.Auth;
using ARPG.GameFlow;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARPG.Networking.Lobby
{
    public enum FusionSessionState
    {
        Idle,
        ConnectingLobby,
        Lobby,
        JoiningRoom,
        InRoom,
        Leaving,
        Error
    }

    /// <summary>
    /// The only project service allowed to create, start, or stop a NetworkRunner.
    /// </summary>
    public sealed class FusionSessionCoordinator : MonoBehaviour
    {
        public const string LobbyName = "arpg-v1";
        private const string PlayScenePath = "Assets/FireBase+Photon/Scenes/Play.unity";

        private readonly FirestoreAuthSessionRepository _sessionRepository =
            new FirestoreAuthSessionRepository();

        private NetworkRunner _runner;
        private NetworkSceneManagerDefault _sceneManager;
        private FusionLobbyService _lobbyService;
        private bool _operationInProgress;

        public static FusionSessionCoordinator Instance { get; private set; }

        public event Action<FusionSessionState, string> StateChanged;
        public event Action<IReadOnlyList<LobbyRoomSnapshot>> RoomsChanged;

        public FusionSessionState State { get; private set; } = FusionSessionState.Idle;
        public string StatusMessage { get; private set; } = "Idle.";
        public IReadOnlyList<LobbyRoomSnapshot> Rooms =>
            _lobbyService != null ? _lobbyService.Rooms : Array.Empty<LobbyRoomSnapshot>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            if (SceneManager.GetActiveScene().name == GameSceneIds.Lobby)
            {
                await JoinLobbyAsync();
            }
        }

        private void OnDestroy()
        {
            DetachLobbyService();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public async Task<bool> JoinLobbyAsync()
        {
            if (_operationInProgress)
            {
                return false;
            }

            _operationInProgress = true;
            try
            {
                return await JoinLobbyInternalAsync();
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public Task<bool> CreateRoomAsync(string roomName)
        {
            return StartRoomAsync(roomName, true, null);
        }

        public Task<bool> JoinRoomAsync(string roomName)
        {
            return StartRoomAsync(roomName, false, null);
        }

        public Task<bool> QuickMatchAsync()
        {
            var filters = new Dictionary<string, SessionProperty>
            {
                { "map", "play" },
                { "phase", "waiting" },
                { "build", Application.version }
            };
            return StartRoomAsync(null, false, filters);
        }

        public async Task<bool> LeaveToLobbyAsync()
        {
            if (_operationInProgress)
            {
                return false;
            }

            _operationInProgress = true;
            SetState(FusionSessionState.Leaving, "Leaving room...");
            try
            {
                await AuthSessionGuard.EndAsync();
                await ShutdownRunnerAsync();

                if (!await EnsureAuthLeaseAsync())
                {
                    FirebaseAuthManager.Instance.SignOut();
                    UserSession.Clear();
                    GameSceneController.LoadLoginMenuScene();
                    return false;
                }

                await GameSceneController.LoadLobbySceneAsync();
                return await JoinLobbyInternalAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetState(FusionSessionState.Error, "Failed to leave the room safely.");
                return false;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public void RefreshRooms()
        {
            if (State == FusionSessionState.Lobby && _lobbyService != null)
            {
                _lobbyService.PublishCurrentRooms();
                SetState(FusionSessionState.Lobby, "Room list is live.");
            }
        }

        private async Task<bool> JoinLobbyInternalAsync()
        {
            if (!await EnsureAuthLeaseAsync())
            {
                SetState(FusionSessionState.Error, "Authentication session is unavailable.");
                return false;
            }

            if (_runner != null && !_runner.IsShutdown && State == FusionSessionState.Lobby)
            {
                RefreshRooms();
                return true;
            }

            if (_runner != null)
            {
                await ShutdownRunnerAsync();
                if (!await EnsureAuthLeaseAsync())
                {
                    SetState(FusionSessionState.Error, "Could not renew the authentication session.");
                    return false;
                }
            }

            CreateRunner();
            SetState(FusionSessionState.ConnectingLobby, "Connecting to lobby...");

            StartGameResult result = await _runner.JoinSessionLobby(SessionLobby.Shared, LobbyName);
            if (!result.Ok)
            {
                SetState(FusionSessionState.Error, FormatFailure("Lobby connection failed", result));
                return false;
            }

            SetState(FusionSessionState.Lobby, "Connected to arpg-v1.");
            return true;
        }

        private async Task<bool> StartRoomAsync(
            string roomName,
            bool createRoom,
            Dictionary<string, SessionProperty> filters)
        {
            if (_operationInProgress)
            {
                return false;
            }

            if (roomName != null && !FusionRoomRules.TryValidateRoomName(roomName, out string error))
            {
                SetState(FusionSessionState.Lobby, error);
                return false;
            }

            if (State != FusionSessionState.Lobby || _runner == null || _runner.IsShutdown)
            {
                SetState(FusionSessionState.Error, "Connect to the lobby before joining a room.");
                return false;
            }

            int playBuildIndex = SceneUtility.GetBuildIndexByScenePath(PlayScenePath);
            if (playBuildIndex < 0)
            {
                SetState(FusionSessionState.Error, "Play scene is missing from Build Settings.");
                return false;
            }

            _operationInProgress = true;
            SetState(FusionSessionState.JoiningRoom, createRoom ? "Creating room..." : "Joining room...");
            try
            {
                string normalizedName = roomName == null
                    ? null
                    : FusionRoomRules.NormalizeRoomName(roomName);
                Dictionary<string, SessionProperty> properties = filters ?? BuildRoomProperties();
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Shared,
                    SessionName = normalizedName,
                    PlayerCount = FusionRoomRules.MaxPlayers,
                    CustomLobbyName = LobbyName,
                    IsOpen = true,
                    IsVisible = true,
                    EnableClientSessionCreation = createRoom,
                    SessionProperties = properties,
                    Scene = SceneRef.FromIndex(playBuildIndex),
                    SceneManager = _sceneManager
                };

                StartGameResult result = await _runner.StartGame(args);
                if (!result.Ok)
                {
                    FusionSessionState fallback = _runner != null && !_runner.IsShutdown
                        ? FusionSessionState.Lobby
                        : FusionSessionState.Error;
                    SetState(fallback, FormatFailure("Room operation failed", result));
                    return false;
                }

                SetState(FusionSessionState.InRoom, $"Joined {(_runner.SessionInfo?.Name ?? normalizedName)}.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetState(FusionSessionState.Error, "Room operation failed unexpectedly.");
                return false;
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        private void CreateRunner()
        {
            GameObject runnerObject = new GameObject("ARPG Network Runner");
            runnerObject.transform.SetParent(transform, false);

            _runner = runnerObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            runnerObject.AddComponent<LegacyPlayerInputSource>();
            runnerObject.AddComponent<FusionPlayerInputCallbacks>();
            _sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
            _lobbyService = runnerObject.AddComponent<FusionLobbyService>();
            runnerObject.AddComponent<FusionAuthSessionBridge>();

            _lobbyService.RoomsChanged += HandleRoomsChanged;
            _lobbyService.ConnectionLost += HandleConnectionLost;
            _lobbyService.SceneLoadCompleted += HandleSceneLoadCompleted;
        }

        private async Task ShutdownRunnerAsync()
        {
            NetworkRunner runner = _runner;
            DetachLobbyService();
            _runner = null;
            _sceneManager = null;
            _lobbyService = null;

            if (runner == null)
            {
                return;
            }

            if (!runner.IsShutdown)
            {
                await runner.Shutdown(false);
            }

            if (runner != null)
            {
                Destroy(runner.gameObject);
            }
        }

        private void DetachLobbyService()
        {
            if (_lobbyService == null)
            {
                return;
            }

            _lobbyService.RoomsChanged -= HandleRoomsChanged;
            _lobbyService.ConnectionLost -= HandleConnectionLost;
            _lobbyService.SceneLoadCompleted -= HandleSceneLoadCompleted;
        }

        private async Task<bool> EnsureAuthLeaseAsync()
        {
            if (AuthSessionGuard.IsActive)
            {
                return true;
            }

            UserData user = UserSession.Current;
            if (user == null || string.IsNullOrWhiteSpace(user.Uid))
            {
                return false;
            }

            string sessionId = Guid.NewGuid().ToString("N");
            AuthSessionResult result = await _sessionRepository.ForceAcquireAsync(user.Uid, sessionId);
            if (!result.Success)
            {
                Debug.LogWarning($"[FusionSession] Lease acquisition failed: {result.Message}");
                return false;
            }

            UserSession.SetUser(user, sessionId);
            AuthSessionGuard.Begin(user.Uid, sessionId);
            return true;
        }

        private static Dictionary<string, SessionProperty> BuildRoomProperties()
        {
            return new Dictionary<string, SessionProperty>
            {
                { "map", "play" },
                { "difficulty", "normal" },
                { "phase", "waiting" },
                { "build", Application.version }
            };
        }

        private void HandleRoomsChanged(IReadOnlyList<LobbyRoomSnapshot> rooms)
        {
            RoomsChanged?.Invoke(rooms);
        }

        private void HandleConnectionLost(string message)
        {
            SetState(FusionSessionState.Error, message);
        }

        private void HandleSceneLoadCompleted(NetworkRunner runner)
        {
            if (runner == null || runner != _runner)
            {
                return;
            }

            PlayerSpawner spawner = FindObjectOfType<PlayerSpawner>();
            if (spawner == null)
            {
                Debug.LogError("[FusionSession] Play scene loaded without a PlayerSpawner.");
                return;
            }

            spawner.EnsureLocalPlayerSpawned(runner);
        }

        private void SetState(FusionSessionState state, string message)
        {
            State = state;
            StatusMessage = message ?? string.Empty;
            StateChanged?.Invoke(state, StatusMessage);
        }

        private static string FormatFailure(string prefix, StartGameResult result)
        {
            string detail = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ShutdownReason.ToString()
                : result.ErrorMessage;
            return $"{prefix}: {detail}";
        }
    }
}
