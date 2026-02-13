# Troubleshooting Guide

Common issues and their solutions for the Corn Hole multiplayer game.

## Installation Issues

### "Photon Fusion namespace not found"

**Symptoms**: Compilation errors, red squiggly lines in scripts

**Solutions**:
1. Verify Photon Fusion is installed:
   - Check Package Manager for Fusion package
   - Look for Fusion folder in Assets or Packages

2. Reimport Fusion:
   - Delete Fusion folder
   - Reimport from Asset Store or package manager

3. Check assembly references:
   - Open CornHole.Scripts.asmdef
   - Verify "Fusion.Runtime" is in references
   - Click "Apply"

4. Restart Unity:
   - Save project
   - Close Unity completely
   - Reopen project

### "TextMeshPro not found"

**Symptoms**: GameUI.cs shows errors

**Solutions**:
1. Import TextMeshPro:
   - Window > TextMeshPro > Import TMP Essential Resources

2. Or remove TMP dependency:
   - Replace `TextMeshProUGUI` with `Text` in GameUI.cs
   - Add `using UnityEngine.UI;`

### "NetworkObject component is missing"

**Symptoms**: Prefabs don't sync across network

**Solutions**:
1. Add NetworkObject to prefabs:
   - Select prefab
   - Add Component > NetworkObject (Fusion)

2. Register prefabs:
   - Create Network Prefab Source (Right-click > Create > Fusion > Network Prefab Source)
   - Add all networked prefabs to it
   - Assign in Project Config

## Connection Issues

### Can't connect to Photon Cloud

**Symptoms**: "Failed to connect" error, timeout

**Solutions**:
1. Verify App ID:
   - Check Photon Dashboard for correct App ID
   - Ensure App ID is set in Fusion settings
   - No extra spaces or characters

2. Check internet connection:
   - Ping photonengine.com
   - Test on different network
   - Check firewall settings

3. Try different region:
   - In Photon settings, change region
   - Try "best", "us", "eu", "asia"

4. Check Photon status:
   - Visit status.photonengine.com
   - Verify cloud is operational

### "AppId not set" error

**Symptoms**: Connection fails immediately

**Solutions**:
1. Set App ID in Photon settings:
   - Fusion > Realtime Settings
   - Or create PhotonAppSettings in Resources folder
   - Paste App ID from dashboard

2. Create settings file manually:
   - Assets > Create > Photon > App Settings
   - Enter App ID
   - Save in Resources folder

### Players can't see each other

**Symptoms**: Both connected, but no other player visible

**Solutions**:
1. Check NetworkObject on prefabs:
   - Player prefab must have NetworkObject
   - NetworkTransform component needed

2. Verify player spawning:
   - Add Debug.Log in OnPlayerJoined
   - Check if prefab is assigned in NetworkManager
   - Ensure prefab is spawned with correct authority

3. Check network synchronization:
   - Verify [Networked] properties in HolePlayer
   - Ensure Runner.Spawn is called with correct parameters

## Gameplay Issues

### Hole doesn't move

**Symptoms**: Player spawns but can't control

**Solutions**:
1. Check input authority:
   - Add Debug.Log(Object.HasInputAuthority) in HolePlayer
   - Should be true for local player

2. Desktop testing:
   - Verify Input.GetAxis works
   - Check Input Manager settings

3. Mobile testing:
   - Test on actual device (not editor)
   - Add Debug.Log(Input.touchCount)
   - Verify touch is registered

### Objects don't fall

**Symptoms**: Consumables spawn but float

**Solutions**:
1. Enable physics:
   - Check Rigidbody on prefab
   - Use Gravity should be true
   - Is Kinematic should be false

2. Check physics settings:
   - Edit > Project Settings > Physics
   - Verify gravity is -9.81 on Y axis

3. State authority:
   - Physics only simulates on state authority
   - Check if ObjectSpawner has authority

### Objects can't be consumed

**Symptoms**: Hole touches object, nothing happens

**Solutions**:
1. Check colliders:
   - Hole needs SphereCollider (Is Trigger: true)
   - Object needs Collider (not trigger)

2. Verify layers:
   - Check Physics collision matrix
   - Ensure layers can interact

3. Check OnTriggerEnter:
   - Add Debug.Log in OnTriggerEnter
   - Verify it's being called
   - Check HasStateAuthority

4. Size check:
   - Verify CanBeConsumed logic
   - Check objectSize vs holeRadius

### Hole doesn't grow

**Symptoms**: Objects consumed but size stays same

**Solutions**:
1. Check networked property:
   - HoleRadius should be [Networked]
   - Verify it's being set on state authority

2. Visual update:
   - Check UpdateHoleScale is called
   - Verify holeVisual is assigned
   - Check scale calculation

## Mobile Issues

### Touch controls don't work

**Symptoms**: No response to touch on device

**Solutions**:
1. Test on device:
   - Touch doesn't work in Unity Editor
   - Must test on actual Android/iOS device

2. Check Input code:
   - Verify #if UNITY_ANDROID || UNITY_IOS
   - Ensure Input.touchCount is checked
   - Add debug logging

3. UI blocking touches:
   - UI Canvas might capture touches
   - Check raycast target settings on UI elements

### Poor performance on mobile

**Symptoms**: Low FPS, stuttering

**Solutions**:
1. Reduce quality:
   - Edit > Project Settings > Quality
   - Select "Low" or "Medium" for mobile
   - Disable shadows if needed

2. Optimize scripts:
   - Cache component references
   - Use object pooling
   - Reduce Update() calls

3. Reduce draw calls:
   - Combine meshes
   - Use fewer materials
   - Reduce particle counts

4. Profile the game:
   - Window > Analysis > Profiler
   - Check CPU usage
   - Identify bottlenecks

### Build errors on Android

**Symptoms**: Build fails with errors

**Solutions**:
1. Check minimum API level:
   - Player Settings > Android
   - Minimum API Level: 22+
   - Target API: Latest stable

2. Use IL2CPP:
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64

3. Gradle issues:
   - Delete Library folder
   - Reimport all
   - Try build again

## Network Synchronization Issues

### Objects sync delayed

**Symptoms**: Laggy movement, delayed actions

**Solutions**:
1. Check tick rate:
   - Fusion settings
   - Higher tick rate = smoother but more bandwidth
   - Default 60 should work

2. Network interpolation:
   - Ensure NetworkTransform is configured
   - Check interpolation settings

3. Test network conditions:
   - High ping can cause delays
   - Test with players in same region
   - Check bandwidth

### Duplicate objects spawning

**Symptoms**: Multiple copies of same object

**Solutions**:
1. Check state authority:
   - Only spawn on state authority
   - Use if (Object.HasStateAuthority)

2. Verify spawn calls:
   - Ensure Runner.Spawn not called multiple times
   - Check ObjectSpawner logic

### Objects disappear randomly

**Symptoms**: Spawned objects vanish

**Solutions**:
1. Check despawn logic:
   - Verify Runner.Despawn conditions
   - Look for unintended despawning

2. Network timeout:
   - Check if objects are being cleaned up
   - Verify object ownership

## UI Issues

### UI not showing

**Symptoms**: Blank screen, no menu

**Solutions**:
1. Check Canvas:
   - Canvas must be in scene
   - Canvas Scaler settings
   - Render Mode: Screen Space - Overlay

2. Verify references:
   - GameUI needs UI element references
   - Check if assigned in Inspector

3. Panel activation:
   - Check which panel is active
   - Menu vs Game panel state

### Buttons don't work

**Symptoms**: Click does nothing

**Solutions**:
1. EventSystem required:
   - Should auto-create with Canvas
   - Check if EventSystem exists in scene

2. Button configuration:
   - OnClick() event assigned
   - Interactable is checked

3. Raycasting:
   - Graphic Raycaster on Canvas
   - UI elements have Raycast Target enabled

## Editor Issues

### Scene won't load

**Symptoms**: GameScene appears empty

**Solutions**:
1. Rebuild scene:
   - Drag objects from prefabs
   - Re-setup lighting
   - Save scene

2. Check .unity file:
   - Open in text editor
   - Verify YAML is valid
   - May need to restore from version control

### Prefabs broken

**Symptoms**: Missing references, pink materials

**Solutions**:
1. Reconnect prefabs:
   - Right-click prefab
   - Select all references
   - Reassign

2. Reimport assets:
   - Right-click Assets folder
   - Reimport All

## Getting More Help

### Debug Logging

Add logging to track issues:
```csharp
Debug.Log($"Player joined: {player}, Authority: {Object.HasStateAuthority}");
Debug.Log($"Touch count: {Input.touchCount}");
Debug.Log($"Hole radius: {HoleRadius}, Score: {Score}");
```

### Unity Console

Check for errors and warnings:
- Window > General > Console
- Look for red errors
- Check stack traces

### Photon Debugging

Enable Fusion logging:
- Add LogLevel settings to NetworkRunner
- Check Photon realtime logs

### Community Resources

- **Unity Forum**: forum.unity.com
- **Photon Forum**: forum.photonengine.com
- **Photon Discord**: discord.gg/photonengine
- **Stack Overflow**: Tag with [unity3d] and [photon]

### File an Issue

If problem persists:
1. Document the issue
2. Include steps to reproduce
3. Attach screenshots/logs
4. Note Unity version and platform
5. Open issue on GitHub repo

## Diagnostic Checklist

When encountering an issue, check:

- [ ] Unity version matches (6.3+)
- [ ] Photon Fusion installed correctly
- [ ] App ID configured
- [ ] Internet connection working
- [ ] Prefabs have NetworkObject
- [ ] Assembly references correct
- [ ] Scene saved
- [ ] Build settings configured
- [ ] Target platform set correctly
- [ ] All references assigned in Inspector
