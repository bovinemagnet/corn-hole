# Corn Hole - Multiplayer Hole.io Game

A 3D multiplayer game inspired by hole.io, built with Unity and Photon Fusion for real-time networking.

## Overview

This is a multiplayer game where players control a hole in the ground that moves around and consumes objects that fall into it. As more objects are consumed, the hole grows bigger, allowing it to eat larger objects and compete with other players.

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

### Key Networking Components

- `NetworkManager`: Handles connection and player spawning
- `HolePlayer`: Networked player with `[Networked]` properties
- `ConsumableObject`: Synchronized consumable objects
- RPCs for effects and events

## Future Enhancements

- [ ] Add more object types and variations
- [ ] Implement player vs player collision mechanics
- [ ] Add power-ups and special abilities
- [ ] Create dedicated server mode
- [ ] Add matchmaking system
- [ ] Implement leaderboards
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
