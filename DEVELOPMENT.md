# Development Guide

## Project Structure

### Scripts Organization

All scripts are in the `CornHole` namespace and located in `Assets/Scripts/`:

- **HolePlayer.cs**: Core player mechanics
  - Movement (touch/keyboard)
  - Hole growth
  - Object consumption
  - Network synchronization

- **ConsumableObject.cs**: Objects that can be eaten
  - Physics-based falling
  - Size checking
  - Network state

- **NetworkManager.cs**: Photon Fusion integration
  - Connection handling
  - Player spawning
  - Session management

- **ObjectSpawner.cs**: Spawns objects into the world
  - Random positioning
  - Timed spawning
  - Object limit management

- **GameUI.cs**: User interface
  - Menu system
  - Score display
  - Connection buttons

- **CameraFollow.cs**: Camera behavior
  - Follows local player
  - Smooth movement

- **GameGround.cs**: Ground plane helper
  - Visual reference
  - Collision surface

## Creating Prefabs

### Player Prefab (HolePlayer)

1. Create empty GameObject named "HolePlayer"
2. Add components:
   ```
   - HolePlayer (script)
   - NetworkObject (Fusion)
   - NetworkTransform (Fusion)
   - SphereCollider (trigger = true)
   ```
3. Create child GameObject "Visual":
   - Add Cylinder mesh (scale: 2, 0.1, 2)
   - Rotate -90 on X axis
   - Add material (dark color)
4. Assign references in HolePlayer:
   - holeVisual = Visual transform
   - consumeCollider = SphereCollider
5. Save as prefab in Assets/Prefabs/

### Consumable Prefab (Cube)

1. Create Cube GameObject
2. Add components:
   ```
   - ConsumableObject (script)
   - NetworkObject (Fusion)
   - Rigidbody
   ```
3. Configure ConsumableObject:
   - objectSize = 0.5
   - pointValue = 10
   - sizeValue = 0.05
4. Configure Rigidbody:
   - Mass = 1
   - Use Gravity = true
5. Add color material
6. Save as prefab
7. Create variants with different sizes

### Scene Setup

1. Open GameScene
2. Add empty GameObject "GameManager":
   - Add NetworkManager script
   - Add ObjectSpawner script
   - Assign prefabs
3. Add Plane for ground (scale 10, 1, 10)
4. Configure camera:
   - Add CameraFollow script
   - Position: (0, 15, -10)
   - Rotation: (45, 0, 0)
5. Create UI Canvas:
   - Add GameUI script
   - Create menu and game panels
   - Add buttons and text elements

## Testing Workflow

### In Editor

1. Set up scene with NetworkManager
2. Create Host build
3. Run build, click "Host Game"
4. In Editor, click Play
5. Click "Join Game"
6. Test synchronization

### Debug Features

Enable Fusion debug:
```csharp
Runner.ProvideInput = true;
[Networked] properties are auto-synced
Use Debug.Log to track state
```

### Mobile Testing

1. Build for Android/iOS
2. Install on device
3. Host on device or Editor
4. Join from other device/Editor
5. Test touch controls

## Code Conventions

- Use `CornHole` namespace for all scripts
- Follow Unity naming conventions
- Use `[Header]` attributes for organization
- Comment public APIs
- Use `[SerializeField]` for inspector fields
- Network properties use `[Networked]`

## Common Patterns

### Networked Property
```csharp
[Networked] public float MyValue { get; set; }
```

### State Authority Check
```csharp
if (Object.HasStateAuthority)
{
    // Only server/host modifies
}
```

### RPC Call
```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_DoSomething()
{
    // Executed on all clients
}
```

### Input Authority Check
```csharp
if (Object.HasInputAuthority)
{
    // Only local player controls
}
```

## Performance Tips

1. **Minimize Network Traffic**:
   - Only sync essential properties
   - Use appropriate tick rates
   - Batch updates when possible

2. **Object Pooling**:
   - Consider pooling consumables
   - Reduce instantiation overhead

3. **Mobile Optimization**:
   - Use LODs for distant objects
   - Optimize physics calculations
   - Reduce draw calls

## Debugging Network Issues

### Connection Problems
- Check App ID
- Verify internet connection
- Check firewall rules
- Try different region

### Synchronization Issues
- Verify NetworkObject on prefabs
- Check [Networked] properties
- Ensure state authority is correct
- Look for RPC errors

### Performance Problems
- Profile with Profiler
- Check network statistics in Fusion
- Monitor tick rate
- Review serialization overhead

## Building for Production

### Android
1. Switch platform to Android
2. Set IL2CPP backend
3. Select ARM64 + ARMv7
4. Configure keystore
5. Build > Build and Run

### iOS
1. Switch platform to iOS
2. Configure bundle identifier
3. Set signing team
4. Build (generates Xcode project)
5. Open in Xcode and build

## Version Control

### Files to Commit
- All scripts (.cs)
- Scene files (.unity)
- Prefabs
- Project settings
- README and docs

### Files to Ignore (.gitignore)
- Library/
- Temp/
- Obj/
- Build/
- Builds/
- Logs/
- .vs/
- *.csproj
- *.sln

## Future Development

### Planned Features
- [ ] Power-ups system
- [ ] Player collision
- [ ] Different object types
- [ ] Visual effects
- [ ] Sound system
- [ ] Leaderboards
- [ ] Matchmaking

### Migration to Dedicated Server
When ready for Server Mode:
1. Create server build (headless)
2. Update NetworkManager for Server mode
3. Deploy to cloud (AWS, Azure, etc.)
4. Update client connection logic
5. Add matchmaking server

## Resources

- Unity Documentation: https://docs.unity3d.com/
- Photon Fusion Docs: https://doc.photonengine.com/fusion
- C# Coding Standards: https://docs.microsoft.com/en-us/dotnet/csharp/
