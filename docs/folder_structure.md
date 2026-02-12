Below is a **simple, scalable Unity folder + “module” layout** that keeps **netcode** and **gameplay** cleanly separated, supports your phased plan (Host → Dedicated), and avoids the classic “everything ends up in Scripts/” mess.

I’ll show:

1. **Folder structure** (what goes where)
2. **Assembly Definition (asmdef) layout** (how to enforce separation)
3. **Key interfaces & class boundaries** (so netcode doesn’t leak into gameplay)
4. **Prefab/scenes conventions** for multiplayer

---

# 1) Recommended folder layout (clean and boring on purpose)

```
Assets/
  _Project/
    Art/
      Materials/
      Models/
      Textures/
      VFX/
      UI/
    Audio/
    Prefabs/
      Common/
      Gameplay/
        Hole/
        Props/
        FX/
      Networking/
        NetworkRunner.prefab
        NetworkPlayer.prefab
        MatchState.prefab
    Scenes/
      Boot/
        Boot.unity
      Menu/
        MainMenu.unity
      Game/
        Game.unity
      Server/
        Server.unity
    Settings/
      Input/
      URP/
      Addressables/          (optional later)
    Scripts/
      Core/                  (engine-agnostic utilities)
      Gameplay/              (pure gameplay logic & rules)
      Presentation/          (VFX/audio/UI view layer)
      Networking/            (Fusion/transport + mapping)
      App/                   (scene flow, bootstrapping)
      Tests/
    ScriptableObjects/
      Balance/
      Maps/
      Audio/
    Docs/
  ThirdParty/
    PhotonFusion/
    ...
```

### Why this works

* **Gameplay** contains rules and data, not Photon types.
* **Networking** contains Photon types, RPCs, snapshots, serialization, runner setup.
* **Presentation** is “juice”: VFX/audio/UI that can change without touching rules.
* **App** wires scenes together and chooses Host vs Dedicated Server mode.

---

# 2) “Modules” via Assembly Definitions (asmdef)

Unity’s asmdefs let you enforce dependencies. This is the easiest way to *prevent* netcode from creeping everywhere.

Create these asmdefs under `Assets/_Project/Scripts/...`:

## Assemblies

1. `_Project.Core`
2. `_Project.Gameplay`
3. `_Project.Presentation`
4. `_Project.Networking`
5. `_Project.App`
6. `_Project.Tests` (optional)

## Dependency rules (important)

* **Gameplay** must not reference Networking.
* **Networking** may reference Gameplay (to call into gameplay services / apply authoritative events).
* **Presentation** references Gameplay (to read state) and optionally Networking (only if it must).
* **App** references everything (it composes the app).

### Allowed dependency graph

```
Core  <- Gameplay <- Networking
Core  <- Gameplay <- Presentation
Core  <- Gameplay <- App
Networking <- App
Presentation <- App
```

### Practical effect

If you accidentally try to import Fusion in Gameplay, it won’t compile. That’s the point.

---

# 3) Clean boundaries: “Gameplay is pure, Networking adapts”

The pattern that keeps this maintainable:

## Gameplay exposes interfaces (no Photon, no MonoBehaviour required)

Gameplay owns:

* rules (eat eligibility, growth curve)
* arbitration logic (distance/radius tie-break)
* match rules (BR vs timed)
* deterministic spawn algorithm (seed → spawn table)
* state containers (player state, consumed bitset)

Networking owns:

* runner setup (Host/Server/Client)
* input collection & sending
* authoritative event broadcasting (ObjectConsumed, PlayerEaten)
* snapshot building and applying (SnapshotFull/Delta)
* reconnect token handling

### Key “ports” between modules

#### In `_Project.Gameplay` (interfaces + data)

```csharp
// Gameplay-only: no Photon types.
public interface IMatchClock {
    int ServerTick { get; }
    float TimeRemainingSeconds { get; }
}

public interface IConsumedSet {
    bool IsConsumed(uint objectId);
    void MarkConsumed(uint objectId);
}

public interface IConsumeArbiter {
    // Given candidates, choose winner deterministically.
    byte ChooseWinner(uint objectId, ReadOnlySpan<Candidate> candidates);
}

public readonly record struct Candidate(byte playerId, float distance, float radius);

public interface ISpawnTableBuilder {
    SpawnTable Build(ulong mapSeed, ushort algoVersion);
}
```

#### In `_Project.Networking` (adapters)

```csharp
// Networking uses Fusion, but calls gameplay services.
// It does not contain game rules, it just enforces authority + sync.
public interface IAuthoritativeEventSink {
    void EmitObjectConsumed(ObjectConsumed evt);
    void EmitPlayerEaten(PlayerEaten evt);
}
```

### Who calls what?

* **ServerSim (Networking)** calls **Gameplay systems** to evaluate:

  * can eat?
  * who wins contested eat?
  * apply growth curve
* Then **Networking** emits events and replicates state.

This keeps the “business logic” testable and engine-agnostic.

---

# 4) Scripts layout (concrete)

## `Scripts/Core/`

Stuff with zero game domain:

```
Core/
  Math/
    FixedPoint.cs              (optional)
    Quantize.cs
  Collections/
    BitSet.cs                  (your consumedSet)
    ChunkedBitSet.cs           (later)
  Diagnostics/
    Log.cs
  Patterns/
    ServiceLocator.cs          (optional; use carefully)
  Util/
    RandomXorShift64.cs
```

## `Scripts/Gameplay/`

No Photon. Minimal MonoBehaviours (ideally none).

```
Gameplay/
  Domain/
    PlayerId.cs
    ObjectId.cs
    MatchMode.cs               (Timed, BR)
  Rules/
    GrowthModel.cs             (area -> radius)
    ConsumeRules.cs            (eligibility)
    KillRules.cs               (BR killMargin)
    ConsumeArbiter.cs          (distance/radius/playerId)
  World/
    SpawnAlgo/
      SpawnTableBuilder.cs
      SpawnTable.cs
      SpawnPoint.cs
    Props/
      PropDef.cs               (requiredRadius, value, prefab key)
  State/
    PlayerState.cs
    MatchState.cs
    ConsumedSetState.cs
  Services/
    MatchService.cs            (apply events to state)
```

## `Scripts/Presentation/`

MonoBehaviours allowed; reads state, plays VFX/audio.

```
Presentation/
  UI/
    MainMenuView.cs
    LobbyView.cs
    HudView.cs
    LeaderboardView.cs
  Camera/
    FollowCamera.cs
  FX/
    ConsumeFxPlayer.cs
    PlayerEatenFxPlayer.cs
  Views/
    HoleView.cs                (visual scale, rim, decal)
    PropView.cs                (cosmetic animation)
```

## `Scripts/Networking/`

All Photon Fusion types and only them.

```
Networking/
  Fusion/
    FusionBootstrap.cs          (runner init)
    FusionSessionService.cs     (create/join by code)
    FusionInputProvider.cs      (collect/send InputFrame)
    FusionPlayerObject.cs       (NetworkBehaviour)
    FusionMatchStateObject.cs   (NetworkBehaviour)
  Authority/
    ServerSim.cs                (tick pipeline)
    SnapshotBuilder.cs
    SnapshotApplier.cs
    ReconnectService.cs
  Events/
    NetEvents.cs                (ObjectConsumed, PlayerEaten structs)
    EventSequencer.cs           (eventSeq monotonic)
  Sync/
    ConsumedSetSync.cs          (bitset/chunks, deltas)
    PropRegistry.cs             (objectId -> PropView mapping)
  Debug/
    NetStatsOverlay.cs
```

## `Scripts/App/`

Orchestration: scene flow, mode selection, config.

```
App/
  Boot/
    BootLoader.cs              (loads config, routes to menu/server)
  Config/
    BuildConfig.cs             (client vs server build toggles)
    NetworkConfig.cs           (tick rate, max players)
    GameConfig.cs              (match length, killMargin)
  Flow/
    SceneFlowController.cs
    GameModeSelector.cs
```

---

# 5) Prefab & scene conventions (to prevent multiplayer spaghetti)

## Scenes

* **Boot.unity**: tiny scene that decides where to go (Menu vs Server)
* **MainMenu.unity**: create/join code UI
* **Game.unity**: gameplay scene used by clients and host
* **Server.unity**: dedicated server scene (no UI, minimal objects)

## Prefabs

* `Prefabs/Networking/NetworkRunner.prefab`

  * contains Fusion runner + bootstrap scripts
* `Prefabs/Networking/NetworkPlayer.prefab`

  * the networked player object (Fusion NetworkBehaviour)
  * references `HoleView` (presentation) but the authority lives in networking
* `Prefabs/Gameplay/Props/*`

  * prop prefabs with simple colliders; presentation handles visuals
* `Prefabs/Gameplay/Hole/*`

  * hole decal/rim visuals and maybe trigger colliders

## One important rule

**Only networking spawns/despawns network objects.**
Gameplay never instantiates prefabs directly. It only requests “spawn X” through a service interface.

---

# 6) A concrete “separation rule” that will save you later

### Gameplay: “pure functions + state updates”

* determines:

  * eligibility
  * growth
  * arbitration winner
* applies:

  * `ApplyObjectConsumed(objectId, eaterId)` → updates state

### Networking: “authority + distribution”

* simulates:

  * movement from inputs
* decides:

  * when to evaluate candidates
* emits:

  * ObjectConsumed events
* syncs:

  * snapshots for joiners/reconnects

### Presentation: “make it feel great”

* plays:

  * suction FX
  * drop tween
  * sounds
* renders:

  * size scaling
  * camera zoom

If you keep those three clean, Phase 6 (dedicated server) becomes a *configuration change*, not a rewrite.

---

# 7) Minimal naming conventions (small but useful)

* All scripts in Networking start with `Fusion` or `Net` (`FusionPlayerObject`, `NetEvents`, `NetSessionService`)
* All pure gameplay classes end with `Rules`, `Model`, `Service`, `State`
* Views end with `View` (`HoleView`, `PropView`, `HudView`)

---

## If you want, I can also provide:

* the **asmdef JSON settings** for each module (copy/paste)
* a minimal **Bootstrap pattern** showing how Boot decides Client vs Dedicated Server build
* a **PropRegistry** pattern for `objectId -> PropView` lookups (needed for ObjectConsumed events)
