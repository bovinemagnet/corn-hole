# Phase 2 Design: LAN Multiplayer Host Mode + Join-Code Lobby

**Date:** 2026-02-13
**Author:** Paul Snow
**Status:** Approved
**Phase:** 2 of 8

---

## Summary

Implement LAN multiplayer using Photon Fusion Host Mode with join-code
lobbies, proper Fusion input system with client-side prediction, a full
lobby UI (player roster, ready state, host start), and synchronised
match lifecycle.

## Approach

**Fusion-native input + session names as join codes.**

- Use Fusion's `INetworkInput` struct with `GetInput()` for client-side
  prediction and rollback
- Use `SessionName` in `StartGameArgs` as the join code — Fusion's
  Photon Cloud matchmaking routes by session name natively
- Full lobby state tracked via networked properties on `MatchTimer` and
  `HolePlayer`
- Player names + ready state as `[Networked]` properties

No external service or custom relay layer required.

---

## Design Sections

### 1. Join Code System

**Changes to `NetworkManager.cs`:**

- `GenerateJoinCode()` produces a random 6-character uppercase
  alphanumeric code (e.g. `"FROG92"`)
- `StartHost()` uses the generated code as `SessionName` in
  `StartGameArgs`
- `JoinGame(string joinCode)` uses the entered code as `SessionName`
- Expose `JoinCode` property for UI to display
- Add error handling for join failures (session not found, full,
  connection failed) via existing `INetworkRunnerCallbacks`

### 2. Fusion Input System (Client-Side Prediction)

**New file: `NetworkInputData.cs`**

```csharp
public struct NetworkInputData : INetworkInput
{
    public Vector2 MoveDirection; // normalised stick input
}
```

**Changes to `NetworkManager.cs`:**

- Implement `OnInput(NetworkRunner, NetworkInput)` callback
- Poll touch (mobile) or keyboard (desktop) input
- Pack into `NetworkInputData` and call `input.Set(data)`

**Changes to `HolePlayer.cs`:**

- Replace direct `Input.GetAxisRaw`/touch reading with
  `GetInput(out NetworkInputData data)`
- Movement runs on all clients with input authority (prediction) — Fusion
  handles rollback/reconciliation
- Remove `[Networked] public Vector3 Position` — rely on
  `NetworkTransform` component for position sync and interpolation
- Keep `[Networked] HoleRadius`, `HoleArea`, `Score` — only modified by
  state authority (consumption logic)
- Normalise input direction server-side to prevent speed cheating

### 3. Lobby System

**Extend `MatchTimer.cs`:**

- MatchPhase enum: `0 = Lobby`, `1 = Countdown`, `2 = Playing`,
  `3 = Ended`
- Add `[Networked] float CountdownTime` for the 3-second pre-match
  countdown
- Host triggers `StartMatch()` → Lobby → Countdown → Playing

**Changes to `HolePlayer.cs`:**

- Add `[Networked] NetworkString<_16> PlayerName` — set on spawn by
  input authority via RPC
- Add `[Networked] NetworkBool IsReady` — toggled by input authority
  via RPC
- Movement disabled when `MatchPhase != Playing`

**Changes to `GameUI.cs`:**

- **Lobby Panel**: join code display, player list with ready indicators,
  ready toggle button, start match button (host only)
- **Join Panel**: text input field for entering join code, join button,
  error display
- **Flow**: Menu → Host → Lobby | Menu → Join → Enter Code → Lobby
- Host's Start button enabled when at least one player is ready (or host
  overrides)

### 4. Match Lifecycle

1. Host clicks "Create Match" → `NetworkManager.StartHost()` → generates
   join code → joins Fusion session → spawns `MatchTimer` in Lobby phase
2. Joiner clicks "Join Match" → enters code →
   `NetworkManager.JoinGame(code)` → connects to session
3. On join, player spawned at spawn point, visible in lobby roster
4. Players toggle ready
5. Host clicks "Start" → `MatchTimer` transitions:
   Lobby → Countdown (3s) → Playing
6. Objects begin spawning (ObjectSpawner gates on Playing phase)
7. Timer counts down → Playing → Ended
8. End screen with final scores → "Return to Menu"

### 5. ObjectSpawner Gate

**Changes to `ObjectSpawner.cs`:**

- Find `MatchTimer` reference on spawn
- Only spawn objects when `MatchTimer.MatchPhase == 2` (Playing)
- Reset spawn timer when match transitions to Playing

### 6. Error Handling

- Join with invalid code: `OnDisconnectedFromServer` or
  `OnConnectFailed` callback → show "Match not found" in UI
- Match full: `OnConnectRequest` refuses → joiner sees
  "Match full" message
- Host disconnects: existing `OnHostMigration` callback logs it;
  full migration is Phase 5+ scope

---

## Files Changed / Created

| File | Action | Key Changes |
|---|---|---|
| `NetworkInputData.cs` | **New** | `INetworkInput` struct |
| `HolePlayer.cs` | **Modify** | Fusion input, player name, ready state, lobby freeze |
| `NetworkManager.cs` | **Modify** | Join code, `OnInput`, error handling |
| `MatchTimer.cs` | **Modify** | Lobby/Countdown phases |
| `GameUI.cs` | **Modify** | Lobby panel, join code input, player roster |
| `ObjectSpawner.cs` | **Modify** | Gate spawning on match phase |

## Out of Scope (Future Phases)

- Deterministic object spawning with map seed (Phase 3)
- Authoritative consume events / contested arbitration (Phase 3)
- Battle royale hole-vs-hole (Phase 4)
- Late join / reconnect (Phase 5)
- Dedicated server mode (Phase 6)

## User Stories Addressed

- **US-2.1**: Create private match with join code
- **US-2.2**: Join match by entering code
- **US-2.3**: Lobby roster and ready state
- **US-2.4**: Authoritative movement over network (inputs only)
- **US-2.5**: Match start/end synchronise across clients
