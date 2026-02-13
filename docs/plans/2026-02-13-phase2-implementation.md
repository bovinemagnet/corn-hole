# Phase 2 Implementation Plan: LAN Multiplayer Host Mode + Join-Code Lobby

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable LAN multiplayer with join codes, proper Fusion input with client-side prediction, full lobby UI, and synchronised match lifecycle.

**Architecture:** Fusion Host Mode with session names as join codes. `INetworkInput` struct polled in `OnInput` callback, consumed via `GetInput()` in `HolePlayer.FixedUpdateNetwork()` for client-side prediction. Lobby state tracked via networked properties on `MatchTimer` (phases) and `HolePlayer` (name, ready). Full lobby panel in `GameUI` with player roster, ready toggle, host start.

**Tech Stack:** Unity 6.3, C# (.NET Standard 2.1), Photon Fusion 2 (Host Mode), TextMeshPro

**Testing note:** This is a Unity project with no command-line test runner. Verification is done in the Unity Editor (Play mode) and standalone builds. Each task includes manual verification steps.

---

## Task 1: Create NetworkInputData struct

**Files:**
- Create: `Assets/Scripts/NetworkInputData.cs`

**Step 1: Create the input struct**

```csharp
using Fusion;
using UnityEngine;

namespace CornHole
{
    /// <summary>
    /// Network input data sent from clients to the host each tick.
    /// Contains only the movement direction — server computes everything else.
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        /// <summary>Normalised movement direction from touch/keyboard input.</summary>
        public Vector2 MoveDirection;
    }
}
```

**Step 2: Verify compilation**

Open Unity Editor. Check Console for zero compilation errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/NetworkInputData.cs
git commit -m "feat: add NetworkInputData struct for Fusion input system"
```

---

## Task 2: Extend MatchTimer with lobby and countdown phases

**Files:**
- Modify: `Assets/Scripts/MatchTimer.cs` (full rewrite)

**Step 1: Rewrite MatchTimer with four phases**

Replace the entire contents of `Assets/Scripts/MatchTimer.cs` with:

```csharp
using System;
using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Manages match lifecycle: Lobby → Countdown → Playing → Ended.
    /// Networked — only the host/state authority modifies phase and timers.
    /// </summary>
    public class MatchTimer : NetworkBehaviour
    {
        public const int PhaseLobby = 0;
        public const int PhaseCountdown = 1;
        public const int PhasePlaying = 2;
        public const int PhaseEnded = 3;

        [Header("Match Settings")]
        [SerializeField] private float matchDurationSeconds = 120f;
        [SerializeField] private float countdownDurationSeconds = 3f;

        /// <summary>Current match phase (see Phase* constants).</summary>
        [Networked] public int MatchPhase { get; set; }

        /// <summary>Time remaining in the current phase (countdown or playing).</summary>
        [Networked] public float RemainingTime { get; set; }

        /// <summary>Fired on all clients when the countdown begins.</summary>
        public event Action OnCountdownStarted;

        /// <summary>Fired on all clients when gameplay starts.</summary>
        public event Action OnMatchStarted;

        /// <summary>Fired on all clients when the match ends.</summary>
        public event Action OnMatchEnded;

        public bool IsLobby => MatchPhase == PhaseLobby;
        public bool IsCountdown => MatchPhase == PhaseCountdown;
        public bool IsPlaying => MatchPhase == PhasePlaying;
        public bool HasEnded => MatchPhase == PhaseEnded;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                // Start in lobby — wait for host to trigger countdown
                MatchPhase = PhaseLobby;
                RemainingTime = 0f;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            if (MatchPhase == PhaseCountdown)
            {
                RemainingTime -= Runner.DeltaTime;
                if (RemainingTime <= 0f)
                {
                    RemainingTime = matchDurationSeconds;
                    MatchPhase = PhasePlaying;
                    RPC_NotifyMatchStarted();
                }
            }
            else if (MatchPhase == PhasePlaying)
            {
                RemainingTime -= Runner.DeltaTime;
                if (RemainingTime <= 0f)
                {
                    RemainingTime = 0f;
                    MatchPhase = PhaseEnded;
                    RPC_NotifyMatchEnded();
                }
            }
        }

        /// <summary>
        /// Called by the host to begin the countdown. Only works from Lobby phase.
        /// </summary>
        public void StartCountdown()
        {
            if (!Object.HasStateAuthority || MatchPhase != PhaseLobby)
                return;

            MatchPhase = PhaseCountdown;
            RemainingTime = countdownDurationSeconds;
            RPC_NotifyCountdownStarted();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyCountdownStarted()
        {
            OnCountdownStarted?.Invoke();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyMatchStarted()
        {
            OnMatchStarted?.Invoke();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyMatchEnded()
        {
            OnMatchEnded?.Invoke();
        }
    }
}
```

**Key changes from the old version:**
- Phase 0 is now Lobby (was auto-starting as Playing)
- Added PhaseCountdown (3s) between Lobby and Playing
- `StartCountdown()` public method for the host to trigger via UI
- Countdown ticks down, then transitions to Playing
- Events for all three transitions

**Step 2: Verify compilation**

Open Unity Editor. Check Console for zero compilation errors. The `GameUI` may show warnings about `HasEnded` / `IsPlaying` — that's fine, we'll update it in Task 6.

**Step 3: Commit**

```bash
git add Assets/Scripts/MatchTimer.cs
git commit -m "feat: extend MatchTimer with lobby and countdown phases"
```

---

## Task 3: Refactor NetworkManager with join codes and input polling

**Files:**
- Modify: `Assets/Scripts/NetworkManager.cs` (significant changes)

**Step 1: Rewrite NetworkManager**

Replace the entire contents of `Assets/Scripts/NetworkManager.cs` with:

```csharp
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
                if (Camera.main != null)
                {
                    Vector3 touchWorldPos = Camera.main.ScreenToWorldPoint(
                        new Vector3(touch.position.x, touch.position.y, Camera.main.transform.position.y));
                    // We need the local player position for direction calculation.
                    // For now, store raw screen direction — HolePlayer will use the
                    // normalised direction from the struct directly.
                    // Touch input calculates direction relative to player in the input struct.
                    direction = new Vector2(touchWorldPos.x, touchWorldPos.z);
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
```

**Key changes from the old version:**
- Removed `StartGame(GameMode)` — replaced with `StartHost()` and `JoinGame(string)`
- `GenerateJoinCode()` produces 6-char code (excludes ambiguous chars like O/0/I/1)
- `SessionName` set to the join code (Fusion routes by session name)
- `PlayerCount` set in `StartGameArgs` for proper capacity
- `OnInput` callback now polls `_polledMoveDirection` and packs into `NetworkInputData`
- `PollInput()` runs in `Update()` — reads touch/keyboard
- Events: `OnConnectionError` and `OnSessionJoined` for UI binding
- Stores `MatchTimer` reference after spawning it
- Touch input stores raw world position — `HolePlayer` handles direction calculation
- Removed unused `gameVersion` field

**Step 2: Verify compilation**

Open Unity Editor. `GameUI.cs` will now have compile errors because it calls the old `StartGame()` method — that's expected, we fix it in Task 6.

**Step 3: Commit**

```bash
git add Assets/Scripts/NetworkManager.cs
git commit -m "feat: add join codes, input polling, and session events to NetworkManager"
```

---

## Task 4: Refactor HolePlayer to use Fusion input with prediction

**Files:**
- Modify: `Assets/Scripts/HolePlayer.cs` (significant changes)

**Step 1: Rewrite HolePlayer**

Replace the entire contents of `Assets/Scripts/HolePlayer.cs` with:

```csharp
using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Network player component for the hole.
    /// Uses Fusion input system for client-side prediction.
    /// Handles movement, area-based growth, and consuming objects.
    /// </summary>
    public class HolePlayer : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float deceleration = 10f;

        [Header("Hole Settings")]
        [SerializeField] private float initialRadius = 1f;
        [SerializeField] private float maxRadius = 10f;

        [Header("References")]
        [SerializeField] private Transform holeVisual;
        [SerializeField] private SphereCollider consumeCollider;

        // Networked properties — synchronised by Fusion
        [Networked] public float HoleRadius { get; set; }
        [Networked] public float HoleArea { get; set; }
        [Networked] public int Score { get; set; }

        // Lobby properties
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }

        // Movement state — networked for rollback prediction
        [Networked] private Vector3 Velocity { get; set; }

        private ObjectSpawner _spawner;
        private MatchTimer _matchTimer;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                HoleRadius = initialRadius;
                HoleArea = Mathf.PI * initialRadius * initialRadius;
                Score = 0;
                IsReady = false;
            }

            _spawner = FindAnyObjectByType<ObjectSpawner>();
            _matchTimer = FindAnyObjectByType<MatchTimer>();
            UpdateHoleScale();
        }

        public override void FixedUpdateNetwork()
        {
            // Only move during the Playing phase
            if (_matchTimer == null)
            {
                _matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            if (_matchTimer != null && _matchTimer.IsPlaying)
            {
                HandleMovement();
            }

            UpdateHoleScale();
        }

        private void HandleMovement()
        {
            if (!GetInput(out NetworkInputData data))
                return;

            float dt = Runner.DeltaTime;

            // Normalise to prevent speed cheating (magnitude clamped to 1)
            Vector2 rawInput = data.MoveDirection;
            if (rawInput.sqrMagnitude > 1f)
            {
                rawInput.Normalize();
            }

            Vector3 inputDirection = new Vector3(rawInput.x, 0f, rawInput.y);
            Vector3 vel = Velocity;

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                // Accelerate towards target velocity
                Vector3 targetVelocity = inputDirection * moveSpeed;
                vel = Vector3.MoveTowards(vel, targetVelocity, acceleration * dt);

                // Rotate towards movement direction
                if (vel.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(vel.normalized);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, targetRotation, rotationSpeed * dt);
                }
            }
            else
            {
                // Decelerate to stop
                vel = Vector3.MoveTowards(vel, Vector3.zero, deceleration * dt);
            }

            Velocity = vel;
            transform.position += vel * dt;
        }

        private void UpdateHoleScale()
        {
            if (holeVisual != null)
            {
                float scale = HoleRadius * 2f; // Diameter
                holeVisual.localScale = new Vector3(scale, 1f, scale);
            }

            if (consumeCollider != null)
            {
                consumeCollider.radius = HoleRadius;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;

            // Only consume during Playing phase
            if (_matchTimer == null || !_matchTimer.IsPlaying)
                return;

            ConsumableObject consumable = other.GetComponent<ConsumableObject>();
            if (consumable != null && consumable.CanBeConsumed(HoleRadius))
            {
                ConsumeObject(consumable);
            }
        }

        private void ConsumeObject(ConsumableObject consumable)
        {
            Score += consumable.PointValue;

            float maxArea = Mathf.PI * maxRadius * maxRadius;
            HoleArea = Mathf.Min(HoleArea + consumable.SizeValue, maxArea);
            HoleRadius = Mathf.Sqrt(HoleArea / Mathf.PI);

            if (_spawner != null)
            {
                _spawner.OnObjectConsumed();
            }

            consumable.Consume(transform.position);
        }

        /// <summary>
        /// Toggle ready state. Called via RPC from input authority.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            IsReady = ready;
        }

        /// <summary>
        /// Set player name. Called via RPC from input authority on spawn.
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetPlayerName(NetworkString<_16> name)
        {
            PlayerName = name;
        }

        public void OnPlayerLeft()
        {
            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
        }
    }
}
```

**Key changes from the old version:**
- `GetInput(out NetworkInputData data)` replaces direct `Input.GetAxisRaw`/touch reading
- Runs on all clients with input (prediction) — Fusion handles rollback
- Removed `[Networked] public Vector3 Position` — the prefab should have a `NetworkTransform` component (already in the checklist) which handles position sync + interpolation
- Added `[Networked] private Vector3 Velocity` for rollback-safe velocity
- Added `PlayerName` and `IsReady` networked properties for lobby
- Added `RPC_SetReady` and `RPC_SetPlayerName` RPCs (input authority → state authority)
- Movement and consumption gated on `MatchTimer.IsPlaying`
- Input normalised server-side to prevent speed cheating

**Step 2: Verify compilation**

Open Unity Editor. Check Console for zero errors in this file. `GameUI.cs` may still error from Task 3 changes — expected.

**Step 3: Commit**

```bash
git add Assets/Scripts/HolePlayer.cs
git commit -m "feat: refactor HolePlayer to use Fusion input with prediction and lobby state"
```

---

## Task 5: Gate ObjectSpawner on match phase

**Files:**
- Modify: `Assets/Scripts/ObjectSpawner.cs:21-41`

**Step 1: Add match phase gate**

Replace the entire contents of `Assets/Scripts/ObjectSpawner.cs` with:

```csharp
using UnityEngine;
using Fusion;

namespace CornHole
{
    /// <summary>
    /// Spawns consumable objects randomly in the game world.
    /// Only spawns during the Playing phase.
    /// </summary>
    public class ObjectSpawner : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private NetworkPrefabRef[] consumablePrefabs;
        [SerializeField] private int maxObjects = 50;
        [SerializeField] private float spawnInterval = 2f;
        [SerializeField] private Vector3 spawnAreaSize = new Vector3(40, 10, 40);
        [SerializeField] private float spawnHeight = 10f;

        [Networked] private TickTimer SpawnTimer { get; set; }
        private int currentObjectCount = 0;
        private MatchTimer _matchTimer;

        public override void Spawned()
        {
            _matchTimer = FindAnyObjectByType<MatchTimer>();

            if (Object.HasStateAuthority)
            {
                SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            // Only spawn objects during the Playing phase
            if (_matchTimer == null)
            {
                _matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            if (_matchTimer == null || !_matchTimer.IsPlaying)
                return;

            if (SpawnTimer.Expired(Runner))
            {
                if (currentObjectCount < maxObjects && consumablePrefabs.Length > 0)
                {
                    SpawnRandomObject();
                }
                SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            }
        }

        private void SpawnRandomObject()
        {
            int prefabIndex = Random.Range(0, consumablePrefabs.Length);
            NetworkPrefabRef prefab = consumablePrefabs[prefabIndex];

            Vector3 randomPos = new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                spawnHeight,
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );

            NetworkObject spawnedObject = Runner.Spawn(prefab, randomPos, Quaternion.identity);

            if (spawnedObject != null)
            {
                currentObjectCount++;
            }
        }

        public void OnObjectConsumed()
        {
            if (currentObjectCount > 0)
            {
                currentObjectCount--;
            }
        }
    }
}
```

**Key changes:**
- Added `_matchTimer` field, found in `Spawned()` and lazily in `FixedUpdateNetwork()`
- Spawning gated on `_matchTimer.IsPlaying` — no objects spawn during Lobby or Countdown
- No other logic changes

**Step 2: Verify compilation**

Open Unity Editor. Check Console for zero errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/ObjectSpawner.cs
git commit -m "feat: gate ObjectSpawner on Playing match phase"
```

---

## Task 6: Rewrite GameUI with lobby, join code input, and player roster

**Files:**
- Modify: `Assets/Scripts/GameUI.cs` (significant rewrite)

**Step 1: Rewrite GameUI**

Replace the entire contents of `Assets/Scripts/GameUI.cs` with:

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace CornHole
{
    /// <summary>
    /// UI manager handling menu, join code entry, lobby, HUD, and end screen.
    /// Flow: Menu → (Host) Lobby | Menu → Join Panel → Lobby → Game → End
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Menu Panel")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;

        [Header("Join Panel")]
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button submitJoinButton;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private TextMeshProUGUI joinErrorText;

        [Header("Lobby Panel")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private TextMeshProUGUI joinCodeDisplay;
        [SerializeField] private TextMeshProUGUI playerListText;
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonLabel;
        [SerializeField] private Button startMatchButton;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Game Panel (HUD)")]
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI sizeText;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("End Screen")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI finalSizeText;
        [SerializeField] private Button returnToMenuButton;

        private NetworkManager _networkManager;
        private HolePlayer _localPlayer;
        private MatchTimer _matchTimer;
        private bool _isReady;

        private void Start()
        {
            _networkManager = FindAnyObjectByType<NetworkManager>();

            // Menu buttons
            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostGame);
            if (joinButton != null)
                joinButton.onClick.AddListener(OnShowJoinPanel);

            // Join panel buttons
            if (submitJoinButton != null)
                submitJoinButton.onClick.AddListener(OnSubmitJoinCode);
            if (backToMenuButton != null)
                backToMenuButton.onClick.AddListener(ShowMenu);

            // Lobby buttons
            if (readyButton != null)
                readyButton.onClick.AddListener(OnToggleReady);
            if (startMatchButton != null)
                startMatchButton.onClick.AddListener(OnStartMatch);

            // End screen
            if (returnToMenuButton != null)
                returnToMenuButton.onClick.AddListener(OnReturnToMenu);

            // Subscribe to network events
            if (_networkManager != null)
            {
                _networkManager.OnConnectionError += ShowJoinError;
                _networkManager.OnSessionJoined += OnSessionJoined;
            }

            ShowMenu();
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnConnectionError -= ShowJoinError;
                _networkManager.OnSessionJoined -= OnSessionJoined;
            }
        }

        private void Update()
        {
            // Lazily find MatchTimer
            if (_matchTimer == null)
            {
                _matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            // Lazily find local player
            if (_localPlayer == null)
            {
                FindLocalPlayer();
            }

            // Update UI based on current match phase
            if (_matchTimer != null)
            {
                if (_matchTimer.IsLobby)
                {
                    UpdateLobbyDisplay();
                }
                else if (_matchTimer.IsCountdown)
                {
                    UpdateCountdownDisplay();
                }
                else if (_matchTimer.IsPlaying)
                {
                    EnsureGamePanel();
                    UpdatePlayerStats();
                    UpdateTimerDisplay();
                }
                else if (_matchTimer.HasEnded)
                {
                    ShowEndScreen();
                }
            }
        }

        private void FindLocalPlayer()
        {
            HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player.Object != null && player.Object.HasInputAuthority)
                {
                    _localPlayer = player;
                    break;
                }
            }
        }

        // --- Menu ---

        private void OnHostGame()
        {
            if (_networkManager != null)
            {
                _networkManager.StartHost();
                // Will transition to lobby via OnSessionJoined callback
            }
        }

        private void OnShowJoinPanel()
        {
            SetAllPanels(false);
            if (joinPanel != null) joinPanel.SetActive(true);
            if (joinErrorText != null) joinErrorText.text = "";
            if (joinCodeInput != null) joinCodeInput.text = "";
        }

        private void OnSubmitJoinCode()
        {
            if (_networkManager == null || joinCodeInput == null)
                return;

            string code = joinCodeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                ShowJoinError("Please enter a join code.");
                return;
            }

            _networkManager.JoinGame(code);
        }

        private void ShowJoinError(string message)
        {
            if (joinErrorText != null)
            {
                joinErrorText.text = message;
            }
        }

        private void OnSessionJoined()
        {
            ShowLobby();
        }

        // --- Lobby ---

        private void ShowLobby()
        {
            SetAllPanels(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);

            // Display join code
            if (joinCodeDisplay != null && _networkManager != null)
            {
                joinCodeDisplay.text = $"Join Code: {_networkManager.JoinCode}";
            }

            // Only host sees the start button
            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(
                    _networkManager != null && _networkManager.IsHost);
            }

            // Reset ready state
            _isReady = false;
            UpdateReadyButtonLabel();

            // Hide countdown text initially
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
            }
        }

        private void UpdateLobbyDisplay()
        {
            // Update player list
            if (playerListText != null)
            {
                HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
                string list = "";
                foreach (var player in players)
                {
                    if (player.Object == null) continue;
                    string name = player.PlayerName.ToString();
                    if (string.IsNullOrEmpty(name)) name = $"Player {player.Object.InputAuthority.PlayerId}";
                    string readyMark = player.IsReady ? " [Ready]" : "";
                    list += $"{name}{readyMark}\n";
                }
                playerListText.text = list;
            }

            // Enable start button only when at least one player is ready
            if (startMatchButton != null && _networkManager != null && _networkManager.IsHost)
            {
                bool anyReady = false;
                HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    if (player.IsReady) { anyReady = true; break; }
                }
                startMatchButton.interactable = anyReady;
            }
        }

        private void OnToggleReady()
        {
            if (_localPlayer == null) return;

            _isReady = !_isReady;
            _localPlayer.RPC_SetReady(_isReady);
            UpdateReadyButtonLabel();
        }

        private void UpdateReadyButtonLabel()
        {
            if (readyButtonLabel != null)
            {
                readyButtonLabel.text = _isReady ? "Not Ready" : "Ready";
            }
        }

        private void OnStartMatch()
        {
            if (_networkManager == null || !_networkManager.IsHost)
                return;

            if (_networkManager.MatchTimer != null)
            {
                _networkManager.MatchTimer.StartCountdown();
            }
        }

        // --- Countdown ---

        private void UpdateCountdownDisplay()
        {
            // Show countdown overlay on lobby
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                int seconds = Mathf.CeilToInt(_matchTimer.RemainingTime);
                countdownText.text = seconds > 0 ? seconds.ToString() : "GO!";
            }

            // Disable lobby buttons during countdown
            if (readyButton != null) readyButton.interactable = false;
            if (startMatchButton != null) startMatchButton.interactable = false;
        }

        // --- Game HUD ---

        private bool _gamePanelShown;

        private void EnsureGamePanel()
        {
            if (!_gamePanelShown)
            {
                SetAllPanels(false);
                if (gamePanel != null) gamePanel.SetActive(true);
                _gamePanelShown = true;
            }
        }

        private void UpdatePlayerStats()
        {
            if (_localPlayer == null) return;

            if (scoreText != null)
                scoreText.text = $"Score: {_localPlayer.Score}";

            if (sizeText != null)
                sizeText.text = $"Size: {_localPlayer.HoleRadius:F1}";
        }

        private void UpdateTimerDisplay()
        {
            if (timerText == null || _matchTimer == null) return;

            float remaining = _matchTimer.RemainingTime;
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        // --- End Screen ---

        private bool _endScreenShown;

        private void ShowEndScreen()
        {
            if (_endScreenShown) return;
            _endScreenShown = true;

            SetAllPanels(false);
            if (endPanel != null) endPanel.SetActive(true);

            if (finalScoreText != null && _localPlayer != null)
                finalScoreText.text = $"Final Score: {_localPlayer.Score}";

            if (finalSizeText != null && _localPlayer != null)
                finalSizeText.text = $"Final Size: {_localPlayer.HoleRadius:F1}";
        }

        // --- Navigation ---

        private void OnReturnToMenu()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ShowMenu()
        {
            SetAllPanels(false);
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        private void SetAllPanels(bool active)
        {
            if (menuPanel != null) menuPanel.SetActive(active);
            if (joinPanel != null) joinPanel.SetActive(active);
            if (lobbyPanel != null) lobbyPanel.SetActive(active);
            if (gamePanel != null) gamePanel.SetActive(active);
            if (endPanel != null) endPanel.SetActive(active);
        }
    }
}
```

**Key changes from the old version:**
- Added **Join Panel** with text input for join code + submit button + error display
- Added **Lobby Panel** with join code display, player list, ready toggle, start button, countdown text
- `OnHostGame()` calls `StartHost()` instead of `StartGame(GameMode.Host)`
- Join flow: enter code → `JoinGame(code)` → OnSessionJoined → ShowLobby
- `Update()` now branches by match phase to show the right panel
- `SetAllPanels(false)` helper to switch between panels cleanly
- Subscribes to `OnConnectionError` and `OnSessionJoined` events from `NetworkManager`
- Player roster updates every frame during Lobby phase
- Countdown overlay shows "3", "2", "1", "GO!" during countdown phase

**Step 2: Verify compilation**

Open Unity Editor. Check Console for **zero compilation errors** across all files.

**Step 3: Commit**

```bash
git add Assets/Scripts/GameUI.cs
git commit -m "feat: rewrite GameUI with lobby, join code input, and player roster"
```

---

## Task 7: Fix touch input for Fusion input system

**Files:**
- Modify: `Assets/Scripts/NetworkManager.cs:95-110` (PollInput method)

The current touch input in `PollInput()` stores a raw world position which won't work correctly — the player doesn't know their own position at poll time. For mobile, we need to convert touch to a direction relative to screen centre (virtual joystick style).

**Step 1: Update PollInput for proper touch direction**

In `Assets/Scripts/NetworkManager.cs`, replace the `PollInput()` method:

```csharp
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
```

**Step 2: Verify compilation**

Open Unity Editor. Check Console for zero errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/NetworkManager.cs
git commit -m "fix: use screen-centre virtual joystick for mobile touch input"
```

---

## Task 8: Integration verification

This task is manual testing in Unity Editor + standalone build.

**Step 1: Verify single-player host flow**

1. Open `Assets/Scenes/GameScene.unity` in Unity Editor
2. Press Play
3. Click "Host Game" on menu
4. Verify: Lobby panel appears with a 6-character join code
5. Verify: Player list shows "Player 0" (or similar)
6. Click "Ready"
7. Verify: Player shows `[Ready]` in roster
8. Click "Start Match"
9. Verify: Countdown shows 3, 2, 1
10. Verify: Game panel appears with timer counting down
11. Verify: Can move hole with arrow keys
12. Verify: Objects start spawning and can be consumed
13. Verify: Timer reaches 0, end screen appears

**Step 2: Verify two-player LAN flow**

1. Build a standalone player: File > Build Settings > Build and Run
2. In the built player, click "Host Game"
3. Note the join code displayed
4. In Unity Editor, press Play
5. Click "Join Match"
6. Enter the join code
7. Click "Join"
8. Verify: Both see the lobby with 2 players listed
9. Both click "Ready"
10. Host clicks "Start Match"
11. Verify: Both see countdown, then gameplay
12. Verify: Both holes are visible and move independently
13. Verify: Consuming objects updates score for the consuming player
14. Verify: Timer ends, both see end screen

**Step 3: Verify error cases**

1. In Unity Editor, press Play
2. Click "Join Match"
3. Enter an invalid code (e.g. "XXXXXX")
4. Click "Join"
5. Verify: Error message appears (connection failed)

**Step 4: Commit any fixes from testing**

If any issues are found, fix them and commit individually.

---

## Task 9: Final commit — update documentation

**Files:**
- Modify: `CHECKLIST.md` (update Phase 2 section)

**Step 1: Update checklist to reflect Phase 2 completion**

Add/update the Phase 2 section in `CHECKLIST.md` to mark multiplayer lobby items as the current checklist items, reflecting the new join code + lobby flow.

**Step 2: Commit**

```bash
git add CHECKLIST.md
git commit -m "docs: update checklist for Phase 2 multiplayer lobby flow"
```

---

## Summary of commits

| # | Message | Files |
|---|---|---|
| 1 | `feat: add NetworkInputData struct for Fusion input system` | `NetworkInputData.cs` |
| 2 | `feat: extend MatchTimer with lobby and countdown phases` | `MatchTimer.cs` |
| 3 | `feat: add join codes, input polling, and session events to NetworkManager` | `NetworkManager.cs` |
| 4 | `feat: refactor HolePlayer to use Fusion input with prediction and lobby state` | `HolePlayer.cs` |
| 5 | `feat: gate ObjectSpawner on Playing match phase` | `ObjectSpawner.cs` |
| 6 | `feat: rewrite GameUI with lobby, join code input, and player roster` | `GameUI.cs` |
| 7 | `fix: use screen-centre virtual joystick for mobile touch input` | `NetworkManager.cs` |
| 8 | *(integration testing — fixes as needed)* | varies |
| 9 | `docs: update checklist for Phase 2 multiplayer lobby flow` | `CHECKLIST.md` |

## Unity Editor tasks (not code — must be done manually)

After the code changes, the following must be done in the Unity Editor:

1. **HolePlayer prefab**: Ensure it has a `NetworkTransform` component (may already exist per checklist)
2. **GameScene**: Add new UI elements to the Canvas for the Join Panel and Lobby Panel (TMP_InputField, Buttons, TextMeshProUGUI elements) and wire them to the `GameUI` component's serialised fields
3. **Network Prefab Source**: Verify all prefabs are registered

These cannot be automated from code — they require the Unity Editor inspector.
