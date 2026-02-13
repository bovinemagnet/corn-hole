# Quick Start Guide

## Getting Started in 5 Minutes

This guide will help you get the Corn Hole game running as quickly as possible.

## Prerequisites

- Unity 2022.3.10f1 or newer installed
- Photon account (free tier is fine)
- Git

## Step-by-Step Setup

### 1. Clone and Open Project

```bash
git clone https://github.com/bovinemagnet/corn-hole.git
cd corn-hole
```

Open Unity Hub, click "Add" and select the cloned folder.

### 2. Install Photon Fusion

**Method 1: Asset Store (Easiest)**
1. Open Unity Asset Store in browser
2. Search "Photon Fusion"
3. Add to My Assets
4. In Unity: Window > Package Manager > My Assets
5. Download and Import Photon Fusion

**Method 2: Unity Package Manager**
1. In Unity: Window > Package Manager
2. Click '+' > Add package from git URL
3. Enter: `https://github.com/photonengine/Fusion.git?path=/Assets/Photon/Fusion`

### 3. Get Photon App ID

1. Visit [Photon Dashboard](https://dashboard.photonengine.com/)
2. Create account or login
3. Click "Create New App"
4. Select Type: "Photon Fusion"
5. Name it: "CornHole"
6. Copy the App ID

### 4. Configure Photon

1. In Unity: Fusion > Realtime Settings (or create PhotonAppSettings in Resources)
2. Paste your App ID
3. Select region (e.g., "us" or "eu")
4. Save

### 5. Create Required Prefabs

**Player Hole Prefab**
1. GameObject > 3D Object > Cylinder (name: "HolePlayer")
2. Transform: Scale (2, 0.1, 2), Rotation X: -90
3. Add Component: `HolePlayer` script
4. Add Component: `NetworkObject` (Fusion)
5. Add Component: `NetworkTransform` (Fusion)
6. Add Component: Sphere Collider (Is Trigger: ✓, Radius: 1)
7. In HolePlayer component:
   - Hole Visual: assign the cylinder's transform
   - Consume Collider: assign the sphere collider
8. Drag to Assets/Prefabs/ to create prefab

**Consumable Cube Prefab**
1. GameObject > 3D Object > Cube (name: "Consumable_Cube")
2. Add Component: `ConsumableObject` script
3. Add Component: `NetworkObject` (Fusion)
4. Add Component: Rigidbody
5. Set ConsumableObject values:
   - Object Size: 0.5
   - Point Value: 10
   - Size Value: 0.05
6. Drag to Assets/Prefabs/ to create prefab

**Create 2-3 variants with different sizes**

### 6. Setup Scene

1. Open Assets/Scenes/GameScene.unity
2. Create Ground:
   - GameObject > 3D Object > Plane
   - Scale: (10, 1, 10)
   - Position: (0, 0, 0)

3. Create GameManager:
   - GameObject > Create Empty (name: "GameManager")
   - Add Component: `NetworkManager`
   - Add Component: `ObjectSpawner`
   - Assign prefabs in Inspector

4. Update Camera:
   - Select Main Camera
   - Add Component: `CameraFollow`

### 7. Test It!

**Test in Editor:**
1. File > Build Settings
2. Add GameScene to Scenes in Build
3. Build and Run (creates standalone executable)
4. In the build: Click "Host Game"
5. In Unity Editor: Click Play
6. Click "Join Game"
7. You should connect!

**Test on Mobile:**
1. Connect Android device via USB
2. File > Build Settings > Android
3. Switch Platform
4. Build and Run
5. Test touch controls

## Troubleshooting

### "Fusion namespace not found"
- Install Photon Fusion package first
- Restart Unity after installation

### "NetworkObject component missing"
- Photon Fusion not installed correctly
- Reimport the package

### Can't connect
- Check App ID is correct
- Verify internet connection
- Try different Photon region
- Check firewall settings

### Touch not working on mobile
- Make sure you're testing on actual device (not editor Play mode)
- Verify Input System is configured

### Prefabs not syncing
- Ensure all networked prefabs have NetworkObject component
- Add prefabs to Network Prefab Source (Fusion settings)

## What You Should See

When working correctly:
- ✅ Both instances connect
- ✅ You see each other's holes moving
- ✅ Objects fall from the sky
- ✅ Holes can eat objects and grow
- ✅ Score increases when eating objects

## Next Steps

Once basic multiplayer works:

1. **Customize Visuals**:
   - Add materials and colors
   - Create particle effects
   - Add skybox and lighting

2. **Enhance Gameplay**:
   - Add more object types
   - Implement player collision
   - Add power-ups

3. **Polish UI**:
   - Create proper menu
   - Add pause functionality
   - Show leaderboard

4. **Mobile Polish**:
   - Optimize performance
   - Add on-screen joystick
   - Implement touch feedback

## Resources

- Full Documentation: See README.md
- Photon Setup: See PHOTON_SETUP.md
- Development Guide: See DEVELOPMENT.md
- Photon Docs: https://doc.photonengine.com/fusion

## Need Help?

- Check DEVELOPMENT.md for detailed guides
- Visit Photon Forum: https://forum.photonengine.com/
- Join Photon Discord: https://discord.gg/photonengine
- Unity Documentation: https://docs.unity3d.com/

---

**Time Investment:**
- Unity Setup: ~5 minutes
- Photon Installation: ~5 minutes
- Prefab Creation: ~10 minutes
- First Test: ~5 minutes
- **Total: ~25 minutes to multiplayer!**
