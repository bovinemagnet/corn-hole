# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Corn Hole is a 3D multiplayer game inspired by hole.io, built with **Unity 6.3** and **Photon Fusion** for real-time networking. Players control a hole that consumes objects and grows larger. Currently at **Phase 2** (LAN multiplayer Host Mode) of an 8-phase roadmap.

- **Language**: C# (.NET Standard 2.1)
- **Namespace**: `CornHole` (all scripts)
- **Platforms**: Android, iOS, Windows, Mac, Linux
- **Licence**: MIT

## Build & Run

This is a Unity project — there is no command-line build system. All building, testing, and running happens through the Unity Editor.

- **Open**: Add the project folder in Unity Hub, open with Unity 6.3+
- **Photon Fusion**: Must be installed separately via Package Manager (git URL or Asset Store import)
- **Photon App ID**: Required — configure at Fusion > Setup > App Id after creating an app at dashboard.photonengine.com
- **Scene**: `Assets/Scenes/GameScene.unity` (index 0 in Build Settings)
- **Prefabs**: Must be created manually in the Unity Editor (see DEVELOPMENT.md for instructions)

### Testing Multiplayer Locally

1. Build a standalone player (File > Build Settings > Build and Run)
2. Click "Host Game" in the built player
3. In Unity Editor, click Play then "Join Game"

## Architecture

### Networking Model

Photon Fusion **Host Mode** — one player acts as server + player, others connect as clients. Designed to migrate to dedicated **Server Mode** in later phases.

Key principle: **server authoritative**. The server/host owns all game state. Clients send only input (stick direction). The server computes positions, consumption, scores, and broadcasts events.

### Core Scripts (`Assets/Scripts/`)

| Script | Role |
|---|---|
| `HolePlayer.cs` | Player controller: movement, growth, object consumption, `[Networked]` properties |
| `NetworkManager.cs` | Photon Fusion lifecycle: connection handling, player spawning/despawning |
| `ConsumableObject.cs` | Physics-based consumable items with size eligibility checks |
| `ObjectSpawner.cs` | Timed random spawning with object limits |
| `GameUI.cs` | Menu/game panels, score display, host/join buttons |
| `CameraFollow.cs` | Smooth camera tracking of local player |
| `GameGround.cs` | Ground plane reference |

### Assembly Definition

Single assembly: `CornHole.Scripts.asmdef` referencing `Fusion.Runtime` and `Unity.TextMeshPro`.

### Key Patterns

```csharp
// Networked state (auto-synchronised by Fusion)
[Networked] public float HoleRadius { get; set; }

// Authority check — only host/server modifies state
if (Object.HasStateAuthority) { /* modify networked state */ }

// Input authority — only local player handles input
if (Object.HasInputAuthority) { /* read input */ }

// RPCs for effects/events
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_PlayConsumeEffect() { }

// Use FixedUpdateNetwork() and Runner.DeltaTime, not Update()/Time.deltaTime
```

### Data Flow

- **Movement**: Input > `HolePlayer.HandleMovement()` > update position > `[Networked]` sync > remote interpolation
- **Consumption**: `OnTriggerEnter` > authority check + size eligibility > update Score/HoleRadius > RPC effect > `Runner.Despawn()`

## Documentation Structure

- **Root-level `.md` files** — practical implementation guides (QUICKSTART, DEVELOPMENT, ARCHITECTURE, PHOTON_SETUP, TROUBLESHOOTING, VISUAL_ASSETS, CHECKLIST)
- **`docs/` folder** — strategic planning (PRD, backlog, architecture overview, authoritative event model, sync strategies, Mermaid diagrams)

### Key Strategic Documents

- `docs/prd-1.md` — full product roadmap (8 phases)
- `docs/overview_rules.md` — authoritative event model, anti-cheat, consume arbitration rules
- `docs/consumedSet_sync.md` — bitset-based object sync strategy for scaling
- `docs/arch_overview.md` — Photon Fusion architecture and Host-to-Server migration path

## Phase Roadmap

| Phase | Focus | Status |
|---|---|---|
| 0-1 | Single-player prototype + production polish | Complete |
| 2 | LAN multiplayer (Host Mode) | In progress |
| 3 | Shared-world authoritative consumption | Planned |
| 4 | Battle royale (hole-vs-hole) | Planned |
| 5 | Late join/reconnect | Planned |
| 6 | Dedicated server (home hardware) | Planned |
| 7-8 | Desktop & console expansion | Planned |

## Conventions

- Use `CornHole` namespace for all scripts
- Use `[SerializeField]` for inspector fields, `[Header]` for organisation
- Network properties use `[Networked]` attribute
- Mobile input: `#if UNITY_ANDROID || UNITY_IOS` preprocessor blocks
- Touch input primary, keyboard/arrow keys as desktop fallback
- Tick rate: 60 Hz default
- Max players: 8 (configurable in NetworkManager)
