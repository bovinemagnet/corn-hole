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


