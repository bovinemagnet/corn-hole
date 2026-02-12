# Architecture Overview

This document describes the architecture and design decisions for the **current implementation** of the Corn Hole multiplayer game.

> **Note**: This describes the Phase 0-2 implementation (Host Mode with basic mechanics). For the complete architectural vision including battle royale, dedicated servers, and advanced sync strategies, see:
> - [docs/arch_overview.md](docs/arch_overview.md) - Full architectural planning
> - [docs/overview_rules.md](docs/overview_rules.md) - Authoritative event model
> - [docs/prd-1.md](docs/prd-1.md) - Product requirements and roadmap

## System Architecture

### High-Level Overview

```
┌─────────────────────────────────────────────────────┐
│                   Client (Unity)                     │
│  ┌────────────┐  ┌─────────────┐  ┌──────────────┐ │
│  │   GameUI   │  │ HolePlayer  │  │ CameraFollow │ │
│  └────────────┘  └─────────────┘  └──────────────┘ │
│  ┌────────────────────────────────────────────────┐ │
│  │        Photon Fusion (Networking Layer)         │ │
│  └────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                           ▲
                           │ Network Communication
                           ▼
┌─────────────────────────────────────────────────────┐
│              Photon Cloud (Relay Server)             │
└─────────────────────────────────────────────────────┘
                           ▲
                           │
                           ▼
┌─────────────────────────────────────────────────────┐
│                   Client (Unity)                     │
│  ┌────────────┐  ┌─────────────┐  ┌──────────────┐ │
│  │   GameUI   │  │ HolePlayer  │  │ CameraFollow │ │
│  └────────────┘  └─────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────┘
```

## Component Architecture

### Core Components

#### 1. NetworkManager
**Responsibility**: Network session lifecycle management

**Key Functions**:
- Initialize Photon Fusion NetworkRunner
- Handle player join/leave events
- Spawn player instances
- Manage network callbacks

**Design Pattern**: Singleton (via DontDestroyOnLoad)

**Network Mode**: Host Mode
- One client acts as both server and player
- Reduces infrastructure cost
- Good for small player counts (2-8 players)

#### 2. HolePlayer
**Responsibility**: Player-controlled hole entity

**Key Features**:
- Movement input handling (mobile touch / keyboard)
- Collision detection for consumables
- Growth mechanics
- Network state synchronization

**State Synchronization**:
```csharp
[Networked] public float HoleRadius { get; set; }  // Synced automatically
[Networked] public int Score { get; set; }          // Synced automatically
[Networked] public Vector3 Position { get; set; }   // Synced automatically
```

**Authority Model**:
- **Input Authority**: Local player controls their own hole
- **State Authority**: Host/Server modifies networked state
- **Proxy**: Remote players receive updates

#### 3. ConsumableObject
**Responsibility**: Objects that can be consumed by holes

**Key Features**:
- Physics simulation (falling)
- Size-based consumption rules
- Network despawn on consumption

**Network Pattern**:
- Spawned by ObjectSpawner (host authority)
- Consumed by any player (relayed through host)
- Despawned via RPC to ensure sync

#### 4. ObjectSpawner
**Responsibility**: Populate game world with consumables

**Key Features**:
- Timed spawning with TickTimer
- Random positioning
- Object limit management

**Design Decision**:
- Only runs on state authority (host)
- Prevents duplicate spawning
- Ensures consistent world state

## Network Architecture

### Photon Fusion Host Mode

**Topology**:
```
Host/Server (Player 1)  ←→  Photon Cloud  ←→  Client (Player 2)
        ↑                                            ↑
    Authoritative                              Receive State
    Simulates Physics
    Spawns Objects
```

**Advantages**:
- Easy setup, no dedicated server needed
- Lower latency for host
- Suitable for mobile games
- Automatic host migration possible

**Trade-offs**:
- Host has advantage (lower latency)
- Limited to ~8-16 players
- Host leaving requires migration

**Migration Path to Server Mode**:
When scaling up, can switch to dedicated server:
1. Create headless Unity build
2. Change GameMode to Server
3. Deploy to cloud (AWS, Azure, etc.)
4. Clients connect as pure clients
5. No local player on server

### State Synchronization

**Networked Properties**:
- Automatically synchronized by Fusion
- Delta compression for bandwidth efficiency
- Interpolation for smooth movement

**Input Handling**:
- Local input processed immediately (prediction)
- Sent to state authority for validation
- State authority broadcasts confirmed state
- Rollback on mismatch (Fusion handles this)

**Object Lifecycle**:
```
1. Spawn Request → State Authority
2. State Authority spawns NetworkObject
3. Fusion replicates to all clients
4. Each client instantiates local copy
5. State updates flow: Authority → Clients
6. Despawn: Authority → Fusion → All Clients
```

## Data Flow

### Player Movement

```
1. Input (Touch/Keyboard)
   ↓
2. HolePlayer.HandleMovement()
   ↓ (if HasStateAuthority)
3. Update transform.position
   ↓
4. Position → [Networked] property
   ↓
5. Fusion synchronizes to remote clients
   ↓
6. Remote: Position property updated
   ↓
7. Remote: transform.position = Position (interpolated)
```

### Object Consumption

```
1. OnTriggerEnter (Local Physics)
   ↓
2. Check HasStateAuthority
   ↓
3. Check CanBeConsumed(holeRadius)
   ↓
4. ConsumeObject() on Authority
   ↓
5. Update Score (networked)
   ↓
6. Update HoleRadius (networked)
   ↓
7. Call consumable.Consume()
   ↓
8. RPC_PlayConsumeEffect() → All clients
   ↓
9. Runner.Despawn(object)
   ↓
10. All clients destroy local instance
```

## Design Patterns

### 1. Component-Based Architecture
Each script has single responsibility:
- HolePlayer: Player logic
- ConsumableObject: Consumable logic
- NetworkManager: Network logic
- ObjectSpawner: Spawning logic

### 2. Authority Pattern
```csharp
if (Object.HasStateAuthority)
{
    // Only host/server executes
    // Modifies networked state
}
else
{
    // Client-side prediction or display
}
```

### 3. RPC Pattern
```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_PlayEffect()
{
    // Executed on all clients
    // For visual/audio feedback
}
```

### 4. Tick-Based Simulation
Uses Fusion's deterministic tick system:
- Fixed timestep (60 Hz default)
- `FixedUpdateNetwork()` instead of `Update()`
- `Runner.DeltaTime` instead of `Time.deltaTime`

## Mobile Considerations

### Platform-Specific Code
```csharp
#if UNITY_ANDROID || UNITY_IOS
    // Touch input
#else
    // Keyboard input
#endif
```

### Performance Optimizations
- IL2CPP scripting backend for better performance
- Quality settings optimized per platform
- Mobile-friendly shaders
- Physics optimization (sleep threshold)

### UI Scaling
- Canvas Scaler for multi-resolution support
- Safe area handling for notched devices
- Touch-friendly UI sizing

## Scalability Considerations

### Current Limits (Host Mode)
- **Players**: 2-8 recommended
- **Objects**: ~50 active consumables
- **Update Rate**: 60 ticks/second

### Optimization Strategies
1. **Object Pooling**: Reuse consumable instances
2. **Spatial Partitioning**: Only sync nearby objects
3. **LOD**: Reduce detail for distant objects
4. **Interest Management**: Only sync relevant entities

### Future Scalability (Server Mode)

For the full scalability and migration plan, see [docs/arch_overview.md](docs/arch_overview.md).

**Planned improvements**:
- Dedicated server can handle 16-32+ players
- Better cheat prevention with server authority
- Persistent world state
- Server-authoritative physics
- Deterministic object spawning (see [docs/consumedSet_sync.md](docs/consumedSet_sync.md))
- Advanced sync with bitset strategy

## Security Considerations

### Current Implementation
- **Host Authority**: Host is trusted
- **State Validation**: Minimal (trust client input)
- **Cheat Prevention**: Limited in Host Mode

### Planned Improvements

See [docs/overview_rules.md](docs/overview_rules.md) for the complete authoritative event model.

**Future security model**:
1. **Server-Side Validation**:
   - Validate movement speed
   - Verify collision events
   - Check consumption rules

2. **Anti-Cheat**:
   - Rate limiting on actions
   - Sanity checks on state changes
   - Logging suspicious behavior

3. **Migration to Server Mode**:
   - Server-authoritative physics
   - Input validation
   - State reconciliation

## Testing Strategy

### Unit Testing
- Individual component logic
- Consumption rules
- Growth calculations

### Integration Testing
- Network synchronization
- Player spawning
- Object lifecycle

### Performance Testing
- Stress test with max players
- Bandwidth usage monitoring
- Mobile device profiling

### Multiplayer Testing
- Local network testing
- Cross-platform (Android ↔ iOS)
- High latency simulation

## Code Organization

### Namespace Structure
```
CornHole/
  - HolePlayer
  - ConsumableObject
  - NetworkManager
  - ObjectSpawner
  - GameUI
  - CameraFollow
  - GameGround
```

### File Organization

**Current simple structure** (suitable for Phase 0-2):
```
Assets/
  Scripts/
    - Core game logic
  Scenes/
    - GameScene
  Prefabs/
    - Networked prefabs
  Materials/
    - Visual materials
  UI/
    - UI assets
```

**Recommended structure for future phases** (from [docs/folder_structure.md](docs/folder_structure.md)):
```
Assets/
  _Project/
    Scripts/
      Core/          - Engine-agnostic utilities
      Gameplay/      - Pure gameplay logic & rules
      Presentation/  - VFX/audio/UI view layer
      Networking/    - Fusion/transport + mapping
      App/           - Scene flow, bootstrapping
    Prefabs/
      Gameplay/
      Networking/
    Scenes/
      Boot/
      Menu/
      Game/
```

This modular structure with assembly definitions keeps netcode and gameplay separated. See [docs/structures.md](docs/structures.md) for complete assembly definition setup.

## Technology Stack

### Core Technologies
- **Unity**: 2022.3 LTS
- **Photon Fusion**: Real-time networking
- **C#**: .NET Standard 2.1

### Supporting Packages
- TextMeshPro: UI text
- Input System: (Optional) New input system
- URP: (Optional) Optimized rendering

## Future Architecture Improvements

This implementation covers **Phase 0-2** foundations. For the complete phased roadmap, see:
- [docs/prd-1.md](docs/prd-1.md) - 8-phase development plan
- [docs/backlog.md](docs/backlog.md) - Detailed user stories per phase

**Key upcoming features**:

### Phase 3: Shared-World Authoritative Consumption
- Server-authoritative object consumption
- Deterministic spawning with shared seed
- consumedSet bitset synchronization
- See [docs/consumedSet_sync.md](docs/consumedSet_sync.md)

### Phase 4: Battle Royale (Hole vs Hole)
- Larger holes consume smaller holes
- Mass inheritance system
- Elimination and respawn mechanics
- See [docs/prd-1.md](docs/prd-1.md) Section 5.4

### Phase 5: Late Join/Reconnect
- Mid-match joining support
- State snapshot delivery
- Reconnection handling
- See [docs/overview_rules.md](docs/overview_rules.md)

### Phase 6: Dedicated Server Mode
- Migration from Host to Server Mode
- Home server deployment (Raspberry Pi)
- Later: Cloud deployment
- See [docs/arch_overview.md](docs/arch_overview.md)

### Phase 7-8: Platform Expansion
- Desktop platforms (Windows/Mac/Linux)
- Console platforms (Nintendo Switch, PS5)
- Cross-platform play

## References

### Implementation Documentation
- [DEVELOPMENT.md](DEVELOPMENT.md) - Code patterns and conventions
- [QUICKSTART.md](QUICKSTART.md) - Getting started guide
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Common issues

### Strategic Documentation
- [docs/README.md](docs/README.md) - Strategic docs index
- [docs/prd-1.md](docs/prd-1.md) - Product requirements
- [docs/arch_overview.md](docs/arch_overview.md) - Full architecture vision

### External Resources
- [Photon Fusion Documentation](https://doc.photonengine.com/fusion)
- [Unity Networking Best Practices](https://docs.unity3d.com/)
- [Game Programming Patterns](https://gameprogrammingpatterns.com/)
