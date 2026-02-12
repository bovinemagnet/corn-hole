Below are the three things you asked for, ready to copy/paste and adapt:

1. **asmdef JSON** for each module (with clean dependencies)
2. **Minimal Boot/scene flow** that chooses Client vs Dedicated Server build/mode
3. **PropRegistry pattern** for `objectId -> PropView` mapping + safe consume handling

I’m going to assume **Unity 2022 LTS+** (or Unity 6), URP optional, and **Fusion** in `Assets/ThirdParty/PhotonFusion/…`.

---

# 1) asmdef JSON files

Create these under:

* `Assets/_Project/Scripts/Core/_Project.Core.asmdef`
* `Assets/_Project/Scripts/Gameplay/_Project.Gameplay.asmdef`
* `Assets/_Project/Scripts/Presentation/_Project.Presentation.asmdef`
* `Assets/_Project/Scripts/Networking/_Project.Networking.asmdef`
* `Assets/_Project/Scripts/App/_Project.App.asmdef`
* `Assets/_Project/Scripts/Tests/_Project.Tests.asmdef` (optional)

> Notes
>
> * In asmdefs, Unity references are by **assembly name**, not folder.
> * Fusion assemblies names vary by version. If you don’t know them yet, leave them out initially, then add after import (Unity inspector shows assembly names).
> * Keep **Gameplay** free of Fusion dependencies.

## `_Project.Core.asmdef`

```json
{
  "name": "_Project.Core",
  "rootNamespace": "Project.Core",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

## `_Project.Gameplay.asmdef`

```json
{
  "name": "_Project.Gameplay",
  "rootNamespace": "Project.Gameplay",
  "references": [
    "_Project.Core"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

## `_Project.Presentation.asmdef`

```json
{
  "name": "_Project.Presentation",
  "rootNamespace": "Project.Presentation",
  "references": [
    "_Project.Core",
    "_Project.Gameplay"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

## `_Project.Networking.asmdef`

> Add Fusion references once imported. Common names you might see: `"Fusion.Runtime"`, `"Fusion.Common"` (varies).
> The key is: Networking can reference Gameplay; Gameplay must not reference Networking.

```json
{
  "name": "_Project.Networking",
  "rootNamespace": "Project.Networking",
  "references": [
    "_Project.Core",
    "_Project.Gameplay"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

## `_Project.App.asmdef`

```json
{
  "name": "_Project.App",
  "rootNamespace": "Project.App",
  "references": [
    "_Project.Core",
    "_Project.Gameplay",
    "_Project.Presentation",
    "_Project.Networking"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

## `_Project.Tests.asmdef` (optional)

If you use Unity Test Runner, you can make this a test-only assembly.

```json
{
  "name": "_Project.Tests",
  "rootNamespace": "Project.Tests",
  "references": [
    "_Project.Core",
    "_Project.Gameplay"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

---

# 2) Minimal Boot flow (Client vs Dedicated Server)

## Goals

* One tiny **Boot** scene that decides:

  * run **Dedicated Server** (headless) → load `Server.unity`
  * run **Client/Menu** → load `MainMenu.unity`
* Works for:

  * local editor runs
  * desktop builds
  * mobile builds
  * headless Linux server builds

## Recommended approach

Use **command-line args** (best for servers) + a ScriptableObject config fallback.

### Folder placement

* `Assets/_Project/Scripts/App/Boot/BootLoader.cs`
* `Assets/_Project/Scripts/App/Config/BuildConfig.cs` (optional)
* Scenes:

  * `Assets/_Project/Scenes/Boot/Boot.unity`
  * `Assets/_Project/Scenes/Menu/MainMenu.unity`
  * `Assets/_Project/Scenes/Server/Server.unity`

### `BootLoader.cs`

```csharp
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.App.Boot
{
    /// <summary>
    /// Boot scene entry point.
    /// Decides whether this build runs as Dedicated Server or as Client.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [Header("Scene names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string serverSceneName   = "Server";

        [Header("Defaults")]
        [SerializeField] private bool defaultToServerInBatchMode = true;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            var args = Environment.GetCommandLineArgs();
            bool serverFlag = HasArg(args, "-server") || HasArg(args, "--server");
            bool clientFlag = HasArg(args, "-client") || HasArg(args, "--client");

            // If batchmode/headless, assume server unless explicitly told otherwise.
            bool isBatch = Application.isBatchMode;

            bool runAsServer =
                serverFlag ||
                (isBatch && defaultToServerInBatchMode && !clientFlag);

            // Optional: allow "-scene Server" style
            string sceneOverride = GetArgValue(args, "-scene") ?? GetArgValue(args, "--scene");

            string sceneToLoad = sceneOverride ?? (runAsServer ? serverSceneName : mainMenuSceneName);

            // Safety: avoid reloading Boot.
            if (SceneManager.GetActiveScene().name == sceneToLoad)
                return;

            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
        }

        private static bool HasArg(string[] args, string arg) =>
            args.Any(a => string.Equals(a, arg, StringComparison.OrdinalIgnoreCase));

        private static string? GetArgValue(string[] args, string arg)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], arg, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }
    }
}
```

### How you run the dedicated server build

Examples:

* `MyGameServer.x86_64 -batchmode -nographics -server`
* Or explicitly: `-server -scene Server`

### Dedicated server scene content (`Server.unity`)

* No UI
* A `FusionBootstrap` object set to **Server Mode**
* Optional: basic console logging + metrics overlay disabled

### Menu scene content (`MainMenu.unity`)

* UI
* A `FusionBootstrap` object set to **Client/Host** as chosen by player

---

# 3) PropRegistry: objectId → PropView mapping (consumed events)

This is the glue that makes your “deterministic world + authoritative consume events” painless.

## Design goals

* Deterministic spawn creates consistent `objectId`s on all clients.
* Each prop registers itself so you can hide it quickly on `ObjectConsumed`.
* Works with pooling (props can be reused).
* Safe if events arrive early/late.

## Suggested pieces

1. **PropView** (Presentation): attaches to prop prefab, owns visuals + hide/show.
2. **PropRegistry** (Networking or Presentation): dictionary mapping `objectId -> PropView`.
3. **WorldSpawner** (Gameplay/Presentation): spawns props from seed and assigns IDs.
4. **ConsumedSet** (Gameplay/Core): tracks which IDs are consumed.

### `PropView.cs` (Presentation)

```csharp
using UnityEngine;

namespace Project.Presentation.Views
{
    public sealed class PropView : MonoBehaviour
    {
        [SerializeField] private Collider[] colliders;
        [SerializeField] private Renderer[] renderers;

        public uint ObjectId { get; private set; }

        public void Init(uint objectId)
        {
            ObjectId = objectId;
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            if (renderers != null)
                foreach (var r in renderers) if (r) r.enabled = visible;

            if (colliders != null)
                foreach (var c in colliders) if (c) c.enabled = visible;

            // Optionally disable GameObject for pooling:
            // gameObject.SetActive(visible);
        }

        public void PlayConsumedFx()
        {
            // Cosmetic: drop tween / particles / sound
            // Keep this here (Presentation), not in Networking.
        }
    }
}
```

### `IPropRegistry` interface (Core or Gameplay)

Put interface in `_Project.Core` or `_Project.Gameplay` so both sides can use it without coupling.

```csharp
namespace Project.Gameplay.World
{
    public interface IPropRegistry
    {
        bool TryGet(uint objectId, out object propView); // keep it generic, or use PropView if Presentation referenced
        void Register(uint objectId, object propView);
        void Unregister(uint objectId);
    }
}
```

But realistically, you can keep the registry in Presentation and just call it from Networking. If you want strict separation, use an interface like above.

### Simple `PropRegistry` (Presentation or Networking)

If you’re okay with Networking referencing Presentation (I’m usually fine with that), do this in Networking. If you want purity, keep it in Presentation and expose interface.

```csharp
using System.Collections.Generic;
using Project.Presentation.Views;
using UnityEngine;

namespace Project.Networking.Sync
{
    public sealed class PropRegistry : MonoBehaviour
    {
        private readonly Dictionary<uint, PropView> _map = new();

        public void Register(PropView view)
        {
            _map[view.ObjectId] = view;
        }

        public void Unregister(PropView view)
        {
            // Remove only if still mapped to this instance (pool safety)
            if (_map.TryGetValue(view.ObjectId, out var cur) && cur == view)
                _map.Remove(view.ObjectId);
        }

        public bool TryGet(uint objectId, out PropView view) =>
            _map.TryGetValue(objectId, out view);

        public void Clear() => _map.Clear();
    }
}
```

### `WorldSpawner.cs` (Presentation/Game scene)

Spawns props deterministically and assigns IDs. It registers each prop.

```csharp
using Project.Presentation.Views;
using Project.Networking.Sync;
using UnityEngine;

namespace Project.Presentation.World
{
    public sealed class WorldSpawner : MonoBehaviour
    {
        [SerializeField] private PropRegistry propRegistry;
        [SerializeField] private Transform propsRoot;

        [Header("Prop Prefabs")]
        [SerializeField] private GameObject[] propPrefabs;

        public void SpawnFromSeed(ulong mapSeed, int count)
        {
            // Deterministic RNG (use your own stable RNG, not UnityEngine.Random)
            var rng = new XorShift64(mapSeed);

            for (uint objectId = 0; objectId < (uint)count; objectId++)
            {
                var prefab = propPrefabs[rng.NextInt(0, propPrefabs.Length)];
                var pos = new Vector3(
                    rng.NextFloat(-40, 40),
                    0,
                    rng.NextFloat(-40, 40)
                );

                var go = Instantiate(prefab, pos, Quaternion.identity, propsRoot);
                var view = go.GetComponent<PropView>();
                view.Init(objectId);

                propRegistry.Register(view);
            }
        }

        // Minimal deterministic RNG helper.
        private struct XorShift64
        {
            private ulong _s;
            public XorShift64(ulong seed) => _s = seed == 0 ? 0xdeadbeefUL : seed;

            public int NextInt(int min, int max)
            {
                ulong x = NextU64();
                return (int)(min + (x % (ulong)(max - min)));
            }

            public float NextFloat(float min, float max)
            {
                ulong x = NextU64();
                // 24-bit mantissa style
                float t = (x & 0xFFFFFF) / (float)0x1000000;
                return min + (max - min) * t;
            }

            private ulong NextU64()
            {
                ulong x = _s;
                x ^= x << 13;
                x ^= x >> 7;
                x ^= x << 17;
                _s = x;
                return x;
            }
        }
    }
}
```

### Applying `ObjectConsumed` events safely

This is where you combine:

* authoritative event stream
* consumed bitset
* registry lookup

```csharp
using Project.Networking.Sync;
using Project.Presentation.Views;
using UnityEngine;

namespace Project.Networking.Events
{
    public sealed class ConsumeEventApplier : MonoBehaviour
    {
        [SerializeField] private PropRegistry propRegistry;

        // Gameplay/core authoritative consumedSet
        private System.Collections.BitArray _consumedBits;

        public void InitConsumedSet(int objectCount)
        {
            _consumedBits = new System.Collections.BitArray(objectCount, false);
        }

        public void ApplyObjectConsumed(uint objectId)
        {
            if (_consumedBits == null) return;

            // Idempotent: safe if duplicate events arrive.
            if (objectId < (uint)_consumedBits.Length)
                _consumedBits[(int)objectId] = true;

            if (propRegistry.TryGet(objectId, out PropView view))
            {
                view.PlayConsumedFx();
                view.SetVisible(false);
            }
            else
            {
                // Event arrived before spawn/registration (rare) or object already pooled.
                // That's okay: consumed bit means it will be hidden when/if it spawns.
            }
        }

        public void ApplySnapshotConsumedSet(byte[] bitsetBytes)
        {
            // For SnapshotFull: replace local bitset and apply to spawned props.
            // Implementation: decode bytes into BitArray, then iterate registry to hide consumed.
        }
    }
}
```

## Handling “event arrives before spawn”

This *will* happen occasionally during joins/reconnects if your ordering isn’t perfect. The rule to make it robust:

* Always update the **consumedSet bits first**.
* Then, if the prop is already registered, hide it.
* If not, when it spawns later, the spawner checks consumedSet and immediately spawns it hidden.

### Add this to `WorldSpawner` after `view.Init(objectId)`

```csharp
// Pseudocode: if consumedSet says it's already eaten, hide it immediately.
if (consumeEventApplier.IsConsumed(objectId))
    view.SetVisible(false);
```

---

# A small opinion that will save you future pain

## Keep PropRegistry and consumedSet separate

* **PropRegistry** = “what objects are currently instantiated and where”
* **consumedSet** = “truth: is objectId alive or consumed”
  They solve different problems. Don’t merge them.

---


Below is a **minimal Photon Fusion 2 skeleton** that matches your folder/module separation and supports:

* **Host (phones/iPads on LAN)**
* **Client join via join-code**
* **Dedicated server later** (headless build / Raspberry Pi in the future)
* **Authoritative server events** (`ObjectConsumed`, `PlayerEaten`)
* **Targeted SnapshotFull** for late-join/reconnect + **consumedSet sync**

This follows Fusion’s documented patterns for `StartGame(StartGameArgs)` and scene loading ([doc.photonengine.com][1]), input polling via `INetworkRunnerCallbacks.OnInput()` ([doc.photonengine.com][2]), and RPCs including **targeted RPCs** using `[RpcTarget] PlayerRef` ([doc.photonengine.com][3]).

---

# 0) Where these files go

```
Assets/_Project/Scripts/Networking/Fusion/
  FusionBootstrap.cs
  FusionSessionService.cs
  FusionInputProvider.cs
  NetworkInputData.cs

Assets/_Project/Scripts/Networking/Events/
  MatchEventsBehaviour.cs
  NetEventTypes.cs

Assets/_Project/Scripts/Networking/Sync/
  ConsumedSetSync.cs
```

> Assumption: you already have your **WorldSpawner / PropRegistry / ConsumeEventApplier** from earlier, living in Presentation/Networking as you prefer.

---

# 1) NetworkInputData (inputs only)

`Assets/_Project/Scripts/Networking/Fusion/NetworkInputData.cs`

```csharp
using Fusion;
using UnityEngine;

namespace Project.Networking.Fusion
{
    // Polled locally by Fusion via INetworkRunnerCallbacks.OnInput() :contentReference[oaicite:3]{index=3}
    public struct HoleInputData : INetworkInput
    {
        public Vector2 Move;              // normalized-ish movement stick
        public NetworkButtons Buttons;    // optional: boosts, emotes, etc.
    }

    // Optional: map buttons to indices (Fusion uses ints for NetworkButtons)
    public static class HoleButtons
    {
        public const int Boost = 0;
        public const int Action = 1;
    }
}
```

---

# 2) FusionInputProvider (single source of OnInput)

`Assets/_Project/Scripts/Networking/Fusion/FusionInputProvider.cs`

```csharp
using Fusion;
using UnityEngine;

namespace Project.Networking.Fusion
{
    /// <summary>
    /// The ONLY place that populates input, to avoid multiple polling sites overwriting input. :contentReference[oaicite:4]{index=4}
    /// </summary>
    public sealed class FusionInputProvider : MonoBehaviour, INetworkRunnerCallbacks
    {
        private Vector2 _move;
        private bool _boost;

        // Call from your touch joystick / input system
        public void SetMove(Vector2 move) => _move = Vector2.ClampMagnitude(move, 1f);
        public void SetBoost(bool pressed) => _boost = pressed;

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new HoleInputData
            {
                Move = _move
            };

            if (_boost)
                data.Buttons.Set(HoleButtons.Boost, true);

            input.Set(data);
        }

        // ---- unused callbacks (keep stubs, or split into a base class)
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
```

---

# 3) Join-code + start/join wrapper (FusionSessionService)

`Assets/_Project/Scripts/Networking/Fusion/FusionSessionService.cs`

```csharp
using System;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Networking.Fusion
{
    /// <summary>
    /// Thin wrapper around runner.StartGame(StartGameArgs) for:
    /// - Host by join-code (SessionName)
    /// - Join by join-code
    /// - Dedicated server mode
    /// </summary>
    public sealed class FusionSessionService : MonoBehaviour
    {
        [SerializeField] private NetworkRunner runnerPrefab;

        public string GenerateJoinCode(int length = 6)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // avoid confusing chars
            var rng = new System.Random();
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
                chars[i] = alphabet[rng.Next(alphabet.Length)];
            return new string(chars);
        }

        public async Task<NetworkRunner> StartHostAsync(string joinCode, bool provideInput = true)
        {
            // StartGame joins/creates a room based on StartGameArgs :contentReference[oaicite:5]{index=5}
            var runner = Instantiate(runnerPrefab);
            runner.name = $"Runner(Host:{joinCode})";
            DontDestroyOnLoad(runner.gameObject);

            runner.ProvideInput = provideInput;

            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Single);

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = joinCode,
                Scene = sceneInfo,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
                              ?? runner.gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (!result.Ok)
            {
                Destroy(runner.gameObject);
                throw new Exception($"StartHost failed: {result.ShutdownReason}");
            }

            return runner;
        }

        public async Task<NetworkRunner> JoinAsync(string joinCode, bool provideInput = true)
        {
            var runner = Instantiate(runnerPrefab);
            runner.name = $"Runner(Client:{joinCode})";
            DontDestroyOnLoad(runner.gameObject);

            runner.ProvideInput = provideInput;

            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Single);

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = joinCode,
                Scene = sceneInfo,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
                              ?? runner.gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (!result.Ok)
            {
                Destroy(runner.gameObject);
                throw new Exception($"Join failed: {result.ShutdownReason}");
            }

            return runner;
        }

        public async Task<NetworkRunner> StartDedicatedServerAsync(string joinCode, bool provideInput = false)
        {
            var runner = Instantiate(runnerPrefab);
            runner.name = $"Runner(Server:{joinCode})";
            DontDestroyOnLoad(runner.gameObject);

            // Dedicated servers have no local player input. Runner.LocalPlayer will be PlayerRef.None. :contentReference[oaicite:6]{index=6}
            runner.ProvideInput = provideInput;

            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Single);

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Server,
                SessionName = joinCode,
                Scene = sceneInfo,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>()
                              ?? runner.gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (!result.Ok)
            {
                Destroy(runner.gameObject);
                throw new Exception($"StartDedicatedServer failed: {result.ShutdownReason}");
            }

            return runner;
        }
    }
}
```

---

# 4) FusionBootstrap (spawn players + spawn match events + late join snapshot)

This is the “BasicSpawner” pattern from Fusion docs, adapted to your game.

`Assets/_Project/Scripts/Networking/Fusion/FusionBootstrap.cs`

```csharp
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Project.Networking.Fusion
{
    public sealed class FusionBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Prefabs")]
        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private NetworkPrefabRef matchEventsPrefab;

        private readonly Dictionary<PlayerRef, NetworkObject> _players = new();
        private NetworkObject _matchEventsObject;

        // Hook this up from your menu code or Boot scene after StartGame completes.
        public void AttachRunner(NetworkRunner runner)
        {
            // Runner auto-finds INetworkRunnerCallbacks in children too :contentReference[oaicite:7]{index=7}
            runner.AddCallbacks(this);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            // Spawn one shared MatchEvents object once.
            if (_matchEventsObject == null)
            {
                _matchEventsObject = runner.Spawn(matchEventsPrefab, Vector3.zero, Quaternion.identity);
            }

            // Spawn player avatar and assign InputAuthority to that player :contentReference[oaicite:8]{index=8}
            var spawnPos = new Vector3((player.RawEncoded % 8) * 3f, 0f, 0f);
            var obj = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);

            _players[player] = obj;

            // Ask MatchEvents to send SnapshotFull to the joining player (targeted RPC).
            var events = _matchEventsObject.GetComponent<Project.Networking.Events.MatchEventsBehaviour>();
            if (events != null)
            {
                events.Server_SendSnapshotTo(player);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            if (_players.TryGetValue(player, out var obj))
            {
                runner.Despawn(obj);
                _players.Remove(player);
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            // IMPORTANT: Fusion runners are single-use; destroy and recreate for next session. :contentReference[oaicite:9]{index=9}
            _players.Clear();
            if (runner != null)
                Destroy(runner.gameObject);
        }

        // ---- stubs
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}
```

---

# 5) Event types + MatchEventsBehaviour (RPCs + targeted snapshot)

## `NetEventTypes.cs`

`Assets/_Project/Scripts/Networking/Events/NetEventTypes.cs`

```csharp
namespace Project.Networking.Events
{
    public readonly struct ObjectConsumedEvent
    {
        public readonly int EventSeq;
        public readonly uint ObjectId;
        public readonly byte EaterId;
        public readonly float EaterAreaAfter;
        public readonly int EaterScoreAfter;

        public ObjectConsumedEvent(int eventSeq, uint objectId, byte eaterId, float eaterAreaAfter, int eaterScoreAfter)
        {
            EventSeq = eventSeq;
            ObjectId = objectId;
            EaterId = eaterId;
            EaterAreaAfter = eaterAreaAfter;
            EaterScoreAfter = eaterScoreAfter;
        }
    }

    public readonly struct PlayerEatenEvent
    {
        public readonly int EventSeq;
        public readonly byte PredatorId;
        public readonly byte PreyId;
        public readonly float PredatorAreaAfter;
        public readonly int PredatorScoreAfter;

        public PlayerEatenEvent(int eventSeq, byte predatorId, byte preyId, float predatorAreaAfter, int predatorScoreAfter)
        {
            EventSeq = eventSeq;
            PredatorId = predatorId;
            PreyId = preyId;
            PredatorAreaAfter = predatorAreaAfter;
            PredatorScoreAfter = predatorScoreAfter;
        }
    }
}
```

## `MatchEventsBehaviour.cs`

`Assets/_Project/Scripts/Networking/Events/MatchEventsBehaviour.cs`

```csharp
using Fusion;
using UnityEngine;

namespace Project.Networking.Events
{
    /// <summary>
    /// One per match (spawned by server). Emits authoritative “punctual events” as RPCs. :contentReference[oaicite:10]{index=10}
    /// Also sends SnapshotFull to joiners via targeted RPC using [RpcTarget] PlayerRef. :contentReference[oaicite:11]{index=11}
    ///
    /// IMPORTANT: RPCs have no remembered state for late joiners, so SnapshotFull is required. :contentReference[oaicite:12]{index=12}
    /// </summary>
    public sealed class MatchEventsBehaviour : NetworkBehaviour
    {
        [SerializeField] private Project.Networking.Sync.ConsumedSetSync consumedSetSync;

        // Simple monotonic counter (you’ll probably move this into a proper sequencer)
        private int _eventSeq;

        public override void Spawned()
        {
            if (consumedSetSync == null)
                consumedSetSync = FindFirstObjectByType<Project.Networking.Sync.ConsumedSetSync>();
        }

        // Called by FusionBootstrap when a player joins.
        public void Server_SendSnapshotTo(PlayerRef target)
        {
            if (!Object.HasStateAuthority)
                return;

            var snap = consumedSetSync.BuildSnapshotFull();
            RPC_SnapshotFull(target, snap.MapSeed, snap.SpawnAlgoVersion, snap.EventSeq, snap.ConsumedBitsetBytes);
        }

        // --- Server calls these when it commits outcomes (your ServerSim tick pipeline)
        public void Server_ObjectConsumed(uint objectId, byte eaterId, float eaterAreaAfter, int eaterScoreAfter)
        {
            if (!Object.HasStateAuthority)
                return;

            int seq = ++_eventSeq;
            RPC_ObjectConsumed(seq, objectId, eaterId, eaterAreaAfter, eaterScoreAfter);
        }

        public void Server_PlayerEaten(byte predatorId, byte preyId, float predatorAreaAfter, int predatorScoreAfter)
        {
            if (!Object.HasStateAuthority)
                return;

            int seq = ++_eventSeq;
            RPC_PlayerEaten(seq, predatorId, preyId, predatorAreaAfter, predatorScoreAfter);
        }

        // Broadcast events to everyone
        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPC_ObjectConsumed(int eventSeq, uint objectId, byte eaterId, float eaterAreaAfter, int eaterScoreAfter)
        {
            consumedSetSync?.Client_ApplyObjectConsumed(eventSeq, objectId, eaterId, eaterAreaAfter, eaterScoreAfter);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPC_PlayerEaten(int eventSeq, byte predatorId, byte preyId, float predatorAreaAfter, int predatorScoreAfter)
        {
            consumedSetSync?.Client_ApplyPlayerEaten(eventSeq, predatorId, preyId, predatorAreaAfter, predatorScoreAfter);
        }

        // Targeted snapshot for late joiners/reconnects:
        // Add a PlayerRef parameter prefaced by [RpcTarget] to target only that player. :contentReference[oaicite:13]{index=13}
        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        private void RPC_SnapshotFull([RpcTarget] PlayerRef target, ulong mapSeed, ushort spawnAlgoVersion, int eventSeq, byte[] consumedBitsetBytes)
        {
            consumedSetSync?.Client_ApplySnapshotFull(mapSeed, spawnAlgoVersion, eventSeq, consumedBitsetBytes);
        }
    }
}
```

---

# 6) ConsumedSetSync (minimal: snapshot + event application hooks)

`Assets/_Project/Scripts/Networking/Sync/ConsumedSetSync.cs`

```csharp
using System;
using UnityEngine;

namespace Project.Networking.Sync
{
    /// <summary>
    /// Owns:
    /// - authoritative consumedSet (server side)
    /// - local consumedSet + application to world (client side)
    ///
    /// This script intentionally does NOT decide who ate what. Your ServerSim does that.
    /// </summary>
    public sealed class ConsumedSetSync : MonoBehaviour
    {
        [Serializable]
        public struct SnapshotFull
        {
            public ulong MapSeed;
            public ushort SpawnAlgoVersion;
            public int EventSeq;
            public byte[] ConsumedBitsetBytes;
        }

        [Header("Match identity")]
        [SerializeField] private ulong mapSeed;
        [SerializeField] private ushort spawnAlgoVersion = 1;

        [Header("World hooks (plug in your own)")]
        [SerializeField] private Project.Networking.Events.ConsumeEventApplier consumeEventApplier; // from earlier
        // Or swap for interfaces if you want strict layering.

        private int _lastEventSeq;

        // Authoritative bitset state (server) would live in a server-only service.
        // For the skeleton, store it here; later move to ServerSim/MatchState.
        private BitArray _consumedBits;

        public void Server_Init(int objectCount, ulong seed, ushort algoVersion)
        {
            mapSeed = seed;
            spawnAlgoVersion = algoVersion;
            _consumedBits = new BitArray(objectCount);
            _lastEventSeq = 0;
        }

        public SnapshotFull BuildSnapshotFull()
        {
            // Serialize BitArray -> byte[] (simple method)
            byte[] bytes = _consumedBits?.ToBytes() ?? Array.Empty<byte>();

            return new SnapshotFull
            {
                MapSeed = mapSeed,
                SpawnAlgoVersion = spawnAlgoVersion,
                EventSeq = _lastEventSeq,
                ConsumedBitsetBytes = bytes
            };
        }

        // ---- called by RPC receivers (client)
        public void Client_ApplySnapshotFull(ulong seed, ushort algoVersion, int eventSeq, byte[] consumedBitsetBytes)
        {
            mapSeed = seed;
            spawnAlgoVersion = algoVersion;
            _lastEventSeq = eventSeq;

            _consumedBits = BitArray.FromBytes(consumedBitsetBytes);

            // Now apply it to the world.
            // You can either:
            // 1) ask WorldSpawner to spawn-from-seed if not spawned yet
            // 2) hide already-spawned props by iterating registry
            // Here we delegate to your ConsumeEventApplier.
            if (consumeEventApplier != null)
                consumeEventApplier.ApplySnapshotConsumedSet(consumedBitsetBytes);
        }

        public void Client_ApplyObjectConsumed(int eventSeq, uint objectId, byte eaterId, float eaterAreaAfter, int eaterScoreAfter)
        {
            // simple gap detection (optional)
            if (eventSeq <= _lastEventSeq)
                return;

            _lastEventSeq = eventSeq;

            consumeEventApplier?.ApplyObjectConsumed(objectId);
            // You’d also update UI/player view using eaterId/areaAfter/scoreAfter.
        }

        public void Client_ApplyPlayerEaten(int eventSeq, byte predatorId, byte preyId, float predatorAreaAfter, int predatorScoreAfter)
        {
            if (eventSeq <= _lastEventSeq)
                return;

            _lastEventSeq = eventSeq;

            // Hook into your BR elimination + mass transfer presentation.
        }

        // --- Tiny BitArray helper (byte-level packing)
        private sealed class BitArray
        {
            private readonly byte[] _bytes;

            private BitArray(int bitCount)
            {
                _bytes = new byte[(bitCount + 7) / 8];
            }

            private BitArray(byte[] bytes)
            {
                _bytes = bytes ?? Array.Empty<byte>();
            }

            public void Set(int bitIndex)
            {
                int b = bitIndex >> 3;
                int o = bitIndex & 7;
                _bytes[b] |= (byte)(1 << o);
            }

            public bool Get(int bitIndex)
            {
                int b = bitIndex >> 3;
                int o = bitIndex & 7;
                return (_bytes[b] & (1 << o)) != 0;
            }

            public byte[] ToBytes() => _bytes;

            public static BitArray FromBytes(byte[] bytes) => new BitArray(bytes);
        }
    }
}
```

---

# 7) Wiring in Unity (the “don’t-think-about-it” checklist)

### A) Create `NetworkRunner.prefab`

1. Empty GameObject: `NetworkRunner`
2. Add components:

   * `NetworkRunner`
   * `NetworkSceneManagerDefault` (Fusion) ([doc.photonengine.com][4])
   * `FusionInputProvider`
   * `FusionBootstrap` (optional: if you want runner prefab to hold it)
3. Save as prefab: `Assets/_Project/Prefabs/Networking/NetworkRunner.prefab`

### B) Create `MatchEvents` prefab

1. Empty GameObject: `MatchEvents`
2. Add components:

   * `NetworkObject`
   * `MatchEventsBehaviour`
3. Save as prefab, assign it to `FusionBootstrap.matchEventsPrefab`

### C) Create `NetworkPlayer` prefab

1. Empty GameObject: `NetworkPlayer`
2. Add:

   * `NetworkObject`
   * (Your `NetPlayerController : NetworkBehaviour` later)
   * Child visuals (HoleView etc.)
3. Save as prefab, assign to `FusionBootstrap.playerPrefab`

### D) Menu wiring

* Menu calls:

  * `session.GenerateJoinCode()`
  * `session.StartHostAsync(code)` or `session.JoinAsync(code)`
* After start succeeds, call `bootstrap.AttachRunner(runner)` (or have bootstrap as child of runner prefab so it auto-registers callbacks).

---

# Two practical implementation notes (so you don’t get bitten later)

1. **Runner is single-use**
   Fusion explicitly notes you can only use a `NetworkRunner` once; after disconnect/shutdown, destroy and create a new runner ([doc.photonengine.com][1]).

2. **RPCs don’t create persistent state**
   Fusion docs warn that late joiners/reconnects “forget” RPC history; therefore you must reconstruct state via `[Networked]` properties and/or a snapshot ([doc.photonengine.com][3]). That’s why the targeted `RPC_SnapshotFull([RpcTarget] PlayerRef target, …)` exists ([doc.photonengine.com][3]).

---

## Next step (if you want me to continue)

The next missing piece is the **authoritative ServerSim tick pipeline** that calls:

* `Server_ObjectConsumed(...)`
* `Server_PlayerEaten(...)`

…and updates `[Networked]` player properties (holeArea/score/alive) in a `NetPlayerController : NetworkBehaviour`.

If you say “go”, I’ll drop in a minimal `NetPlayerController` that:

* reads `HoleInputData` each tick,
* moves with server authority,
* calls your ConsumeArbiter,
* emits the events through `MatchEventsBehaviour`.

[1]: https://doc.photonengine.com/fusion/current/manual/network-runner "Fusion 2 - Network Runner | Photon Engine"
[2]: https://doc.photonengine.com/fusion/current/manual/data-transfer/player-input?utm_source=chatgpt.com "Client-Server Player Input - Photon Fusion 2"
[3]: https://doc.photonengine.com/fusion/current/manual/data-transfer/rpcs "Fusion 2 - Remote Procedure Calls | Photon Engine"
[4]: https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/2-setting-up-a-scene "Fusion 2 - 2 - Setting Up A Scene | Photon Engine"
