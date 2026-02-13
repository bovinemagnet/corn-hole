using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;

namespace CornHole
{
    /// <summary>
    /// Main network manager for the game.
    /// Handles Photon Fusion connection, join codes, input polling, and player spawning.
    /// Uses Host Mode (one player is server + player).
    /// </summary>
    public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Network Settings")]
        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private NetworkPrefabRef matchTimerPrefab;
        [SerializeField] private int maxPlayers = 8;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3[] spawnPoints = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 10),
            new Vector3(-10, 0, 10),
            new Vector3(10, 0, -10),
            new Vector3(-10, 0, -10),
            new Vector3(0, 0, 15),
            new Vector3(0, 0, -15),
            new Vector3(15, 0, 0)
        };

        /// <summary>The join code for the current session. Null if not hosting.</summary>
        public string JoinCode { get; private set; }

        /// <summary>Fired when connection fails or is rejected. Carries an error message.</summary>
        public event Action<string> OnConnectionError;

        /// <summary>Fired when successfully connected to a session.</summary>
        public event Action OnSessionJoined;

        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
        private bool _matchTimerSpawned;

        // Input state — polled in Update, consumed in OnInput
        private Vector2 _polledMoveDirection;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            PollInput();
        }

        /// <summary>
        /// Host a new game. Generates a join code and starts a Fusion session.
        /// </summary>
        public async void StartHost()
        {
            JoinCode = GenerateJoinCode();
            Debug.Log($"Hosting with join code: {JoinCode}");

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = JoinCode,
                PlayerCount = maxPlayers,
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (result.Ok)
            {
                Debug.Log($"Hosting session: {JoinCode}");
                OnSessionJoined?.Invoke();
            }
            else
            {
                Debug.LogError($"Failed to host: {result.ShutdownReason}");
                OnConnectionError?.Invoke($"Failed to create match: {result.ShutdownReason}");
            }
        }

        /// <summary>
        /// Join an existing game by its join code.
        /// </summary>
        public async void JoinGame(string joinCode)
        {
            JoinCode = joinCode.ToUpper().Trim();
            Debug.Log($"Joining with code: {JoinCode}");

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = JoinCode,
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (result.Ok)
            {
                Debug.Log($"Joined session: {JoinCode}");
                OnSessionJoined?.Invoke();
            }
            else
            {
                Debug.LogError($"Failed to join: {result.ShutdownReason}");
                OnConnectionError?.Invoke($"Failed to join match: {result.ShutdownReason}");
            }
        }

        /// <summary>Whether this instance is the host/server.</summary>
        public bool IsHost => _runner != null && _runner.IsServer;

        /// <summary>Reference to the MatchTimer once spawned.</summary>
        public MatchTimer MatchTimer { get; private set; }

        private string GenerateJoinCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var code = new char[6];
            for (int i = 0; i < code.Length; i++)
            {
                code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            }
            return new string(code);
        }

        private void PollInput()
        {
            Vector2 direction = Vector2.zero;

#if UNITY_ANDROID || UNITY_IOS
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                // Treat touch as virtual joystick: direction from screen centre
                Vector2 screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 touchDelta = touch.position - screenCentre;
                // Normalise with a dead zone
                float maxRadius = Screen.height * 0.3f;
                if (touchDelta.magnitude > 20f) // dead zone in pixels
                {
                    direction = touchDelta / maxRadius;
                    if (direction.sqrMagnitude > 1f)
                    {
                        direction.Normalize();
                    }
                }
            }
#else
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            direction = new Vector2(horizontal, vertical);
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }
#endif

            _polledMoveDirection = direction;
        }

        private Vector3 GetSpawnPoint(PlayerRef player)
        {
            int index = player.PlayerId % spawnPoints.Length;
            return spawnPoints[index];
        }

        #region INetworkRunnerCallbacks Implementation

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                Debug.Log($"Player {player} joined");

                // Spawn match timer once (first player triggers it)
                if (!_matchTimerSpawned)
                {
                    var timerObj = runner.Spawn(matchTimerPrefab, Vector3.zero, Quaternion.identity);
                    MatchTimer = timerObj.GetComponent<MatchTimer>();
                    _matchTimerSpawned = true;
                }

                // Spawn player at a spawn point
                Vector3 spawnPosition = GetSpawnPoint(player);
                NetworkObject networkPlayerObject = runner.Spawn(
                    playerPrefab, spawnPosition, Quaternion.identity, player);
                _spawnedPlayers[player] = networkPlayerObject;
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_spawnedPlayers.TryGetValue(player, out NetworkObject networkObject))
            {
                runner.Despawn(networkObject);
                _spawnedPlayers.Remove(player);
            }

            Debug.Log($"Player {player} left");
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData
            {
                MoveDirection = _polledMoveDirection
            };
            input.Set(data);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Connected to server");
            OnSessionJoined?.Invoke();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"Disconnected from server: {reason}");
            OnConnectionError?.Invoke($"Disconnected: {reason}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"Connect failed: {reason}");
            OnConnectionError?.Invoke($"Connection failed: {reason}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (_spawnedPlayers.Count < maxPlayers)
            {
                request.Accept();
            }
            else
            {
                request.Refuse();
            }
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"Shutdown: {shutdownReason}");
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            Debug.Log("Host migration started");
        }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        #endregion
    }
}
