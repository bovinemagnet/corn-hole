# Corn Hole - Multiplayer Hole.io Game

A 3D multiplayer game inspired by hole.io, built with Unity and Photon Fusion for real-time networking.

## 📚 Documentation

This project has two complementary documentation sets:

- **Implementation Guides** (this directory): Practical, hands-on guides for building and running the game now
- **Strategic Planning** ([docs/ folder on main branch](../../tree/main/docs)): Architectural vision, PRD, and long-term roadmap

**New to the project?** Start with [QUICKSTART.md](QUICKSTART.md) for a 25-minute setup guide.

**Want to understand the full vision?** See [docs/prd-1.md](../../blob/main/docs/prd-1.md) and [docs/arch_overview.md](../../blob/main/docs/arch_overview.md) on the main branch.

## Overview

This is a multiplayer game where players control a hole in the ground that moves around and consumes objects that fall into it. As more objects are consumed, the hole grows bigger, allowing it to eat larger objects and compete with other players.

**Current Implementation**: This PR implements **Phase 0-1** foundations from the project roadmap - basic Host Mode multiplayer with core consumption mechanics. Future phases will add battle royale features, dedicated servers, and advanced networking (see docs/ folder for details).

## Features

- **Multiplayer Networking**: Uses Photon Fusion in Host Mode (one player acts as server + player)
- **Mobile Support**: Designed for Android and iOS with touch controls
- **Real-time Synchronization**: Player positions, hole sizes, and object states are synchronized across all clients
- **Growth Mechanics**: Holes grow as they consume objects
- **Scalable Architecture**: Can be migrated from Host Mode to dedicated Server Mode in the future

## Technology Stack

- **Engine**: Unity 2022.3.10f1
- **Networking**: Photon Fusion
- **Platform**: Android, iOS (with desktop support for testing)
- **Language**: C#

## Project Structure

```
Assets/
├── Scenes/
│   └── GameScene.unity       # Main game scene
├── Scripts/
│   ├── HolePlayer.cs         # Player-controlled hole mechanics
│   ├── ConsumableObject.cs   # Objects that can be eaten
│   ├── NetworkManager.cs     # Photon Fusion network setup
│   ├── ObjectSpawner.cs      # Spawns consumable objects
│   └── GameUI.cs             # UI management
└── Prefabs/                  # Game object prefabs (to be created in Unity Editor)
```

## Setup Instructions

### Prerequisites

1. Unity 2022.3.10f1 or newer
2. Photon Fusion SDK (install via Unity Package Manager or Asset Store)
3. TextMeshPro (included in Unity packages)

### Installation

1. Clone this repository
2. Open the project in Unity
3. Install Photon Fusion:
   - Open Package Manager (Window > Package Manager)
   - Click '+' > Add package from git URL
   - Enter: `https://github.com/photonengine/Fusion.git?path=/Assets/Photon/Fusion`
   - Or download from Photon Engine website and import
4. Set up Photon App ID:
   - Go to [Photon Dashboard](https://dashboard.photonengine.com/)
   - Create a new Fusion app
   - Copy the App ID
   - In Unity, go to Fusion > Setup > App Id
   - Paste your App ID

### Creating Prefabs

You need to create the following prefabs in Unity Editor:

1. **Player Hole Prefab**:
   - Create a GameObject with HolePlayer component
   - Add a visual representation (e.g., a flat cylinder or plane)
   - Add a SphereCollider (set as trigger)
   - Add NetworkObject and NetworkTransform components
   - Save as prefab

2. **Consumable Object Prefabs**:
   - Create GameObjects with ConsumableObject component
   - Add visual mesh (cube, sphere, etc.)
   - Add Rigidbody
   - Add Collider
   - Add NetworkObject component
   - Create variants with different sizes and point values
   - Save as prefabs

3. **Network Manager**:
   - Create empty GameObject in the scene
   - Add NetworkManager component
   - Assign player and consumable prefabs
   - Add ObjectSpawner component if desired

### Building for Mobile

#### Android

1. File > Build Settings
2. Select Android platform
3. Switch Platform
4. Player Settings:
   - Set minimum API level to 22 or higher
   - Configure package name
   - Set scripting backend to IL2CPP
   - Select target architectures (ARM64, ARMv7)
5. Build and Run

#### iOS

1. File > Build Settings
2. Select iOS platform
3. Switch Platform
4. Player Settings:
   - Set minimum iOS version to 11.0 or higher
   - Configure bundle identifier
5. Build (requires macOS with Xcode)

## How to Play

1. **Start a Game**:
   - Host a game (becomes server + player)
   - Or join an existing game

2. **Controls**:
   - **Mobile**: Touch and drag to move the hole
   - **Desktop**: Use arrow keys or WASD

3. **Objective**:
   - Move your hole to consume falling objects
   - Grow bigger by eating more objects
   - Compete with other players for the highest score

## Networking Architecture

The game uses Photon Fusion in **Host Mode**:
- One player acts as both server and client
- Advantages: Easy setup, no dedicated server needed
- Migration path: Can move to Server Mode later for dedicated servers

For the full architectural vision including battle royale mechanics, dedicated servers, and advanced sync strategies, see:
- [docs/arch_overview.md](../../blob/main/docs/arch_overview.md) - Complete architectural overview
- [docs/overview_rules.md](../../blob/main/docs/overview_rules.md) - Authoritative event model
- [docs/consumedSet_sync.md](../../blob/main/docs/consumedSet_sync.md) - Advanced object sync strategy

### Key Networking Components

- `NetworkManager`: Handles connection and player spawning
- `HolePlayer`: Networked player with `[Networked]` properties
- `ConsumableObject`: Synchronized consumable objects
- RPCs for effects and events

## Roadmap & Future Enhancements

This implementation covers **Phase 0-1** of the project plan. For the complete roadmap, see:
- [docs/prd-1.md](../../blob/main/docs/prd-1.md) - Product Requirements Document
- [docs/backlog.md](../../blob/main/docs/backlog.md) - User stories and acceptance criteria
- [docs/one-page-checklist](../../blob/main/docs/one-page-checklist) - Phase-by-phase implementation guide

**Planned features** (from strategic docs):
- [ ] Battle royale: Holes can consume smaller holes and inherit mass
- [ ] Join-code based matchmaking for private games
- [ ] Late join and reconnect support
- [ ] Dedicated server mode (home server → cloud deployment)
- [ ] Deterministic object spawning with shared seed
- [ ] Advanced sync with consumedSet bitset strategy
- [ ] Console platform support (Nintendo Switch, PlayStation 5)

**Near-term enhancements** (implementation level):
- [ ] Add more object types and variations
- [ ] Implement power-ups and special abilities
- [ ] Add sound effects and music
- [ ] Create visual effects for consuming objects
- [ ] Add minimap
- [ ] Implement different game modes

## Troubleshooting

### Common Issues

1. **Photon Connection Failed**:
   - Verify App ID is correctly set
   - Check internet connection
   - Ensure firewall allows connection

2. **Mobile Controls Not Working**:
   - Verify Input System is configured
   - Check touch input code matches your Unity version

3. **Objects Not Syncing**:
   - Ensure NetworkObject component is on all networked prefabs
   - Verify prefabs are registered in Fusion settings

## License

This project is provided as-is for educational and development purposes.

## Credits

- Built with Unity
- Networking powered by Photon Fusion
- Inspired by hole.io
