# Implementation Checklist

Use this checklist to set up and test the Corn Hole multiplayer game.

## ☐ Initial Setup

### Prerequisites
- [ ] Unity 2022.3.10f1 (or newer LTS) installed
- [ ] Unity Hub installed
- [ ] Git installed
- [ ] Code editor (Visual Studio, VS Code, or Rider)

### Project Setup
- [ ] Clone repository: `git clone https://github.com/bovinemagnet/corn-hole.git`
- [ ] Open Unity Hub
- [ ] Add project from disk (select cloned folder)
- [ ] Open project in Unity
- [ ] Wait for initial import (may take 2-5 minutes)
- [ ] Verify no compilation errors in Console

---

## ☐ Photon Fusion Installation

### Get Photon Fusion
- [ ] Option A: Download from Unity Asset Store
  - [ ] Search for "Photon Fusion"
  - [ ] Add to My Assets
  - [ ] Open Package Manager in Unity
  - [ ] Find in My Assets and import
- [ ] Option B: Install via Package Manager
  - [ ] Window > Package Manager
  - [ ] '+' > Add package from git URL
  - [ ] Enter Fusion git URL
  - [ ] Click Add

### Verify Installation
- [ ] Check for Fusion folder in Packages or Assets
- [ ] Look for "Fusion" menu in Unity menu bar
- [ ] No compilation errors related to Fusion
- [ ] Restart Unity if needed

---

## ☐ Photon Configuration

### Get App ID
- [ ] Go to https://dashboard.photonengine.com/
- [ ] Sign up or log in
- [ ] Click "Create New App"
- [ ] Select "Photon Fusion" as app type
- [ ] Name: "CornHole" (or your choice)
- [ ] Click Create
- [ ] Copy the App ID

### Configure in Unity
- [ ] In Unity: Fusion > Realtime Settings (or similar)
- [ ] Paste App ID
- [ ] Select region (e.g., "us" or "eu")
- [ ] Save settings
- [ ] Verify no "App ID not set" warnings

---

## ☐ Create Prefabs

### HolePlayer Prefab
- [ ] Create empty GameObject, name: "HolePlayer"
- [ ] Add HolePlayer component (script)
- [ ] Add NetworkObject component (Fusion)
- [ ] Add NetworkTransform component (Fusion)
- [ ] Add SphereCollider component
  - [ ] Set Is Trigger: ✓
  - [ ] Set Radius: 1
- [ ] Create child GameObject: "Visual"
- [ ] Add Cylinder mesh to Visual
  - [ ] Scale: (2, 0.1, 2)
  - [ ] Rotation: (-90, 0, 0)
- [ ] Create dark material, assign to cylinder
- [ ] In HolePlayer component:
  - [ ] Assign Hole Visual: drag Visual transform
  - [ ] Assign Consume Collider: drag SphereCollider
- [ ] Drag HolePlayer to Assets/Prefabs/ (creates prefab)
- [ ] Delete from scene

### Consumable Prefabs

**Small Cube (10 points)**
- [ ] Create Cube GameObject: "Consumable_Small"
- [ ] Scale: 0.5
- [ ] Add ConsumableObject component
  - [ ] Object Size: 0.5
  - [ ] Point Value: 10
  - [ ] Size Value: 0.05
- [ ] Add NetworkObject component
- [ ] Add Rigidbody component
  - [ ] Use Gravity: ✓
- [ ] Create green material, assign to cube
- [ ] Drag to Assets/Prefabs/ (creates prefab)
- [ ] Delete from scene

**Medium Cube (25 points)** - Optional
- [ ] Repeat above with:
  - [ ] Scale: 1.0
  - [ ] Object Size: 1.0
  - [ ] Point Value: 25
  - [ ] Size Value: 0.1
  - [ ] Yellow material

**Large Sphere (50 points)** - Optional
- [ ] Create Sphere: "Consumable_Large"
- [ ] Scale: 1.5
- [ ] Configure ConsumableObject (size: 1.5, points: 50)
- [ ] Add NetworkObject and Rigidbody
- [ ] Red material
- [ ] Save as prefab

### Network Prefab Source
- [ ] Right-click in Project > Create > Fusion > Network Prefab Source
- [ ] Name: "NetworkPrefabs"
- [ ] Add all created prefabs to the list:
  - [ ] HolePlayer
  - [ ] Consumable_Small
  - [ ] Any other consumables

---

## ☐ Scene Setup

### Open Scene
- [ ] Open Assets/Scenes/GameScene.unity
- [ ] Save scene if modified

### Create Ground
- [ ] GameObject > 3D Object > Plane
- [ ] Name: "Ground"
- [ ] Position: (0, 0, 0)
- [ ] Scale: (10, 1, 10)
- [ ] Add ground material (optional)

### Setup GameManager
- [ ] GameObject > Create Empty
- [ ] Name: "GameManager"
- [ ] Add NetworkManager component
  - [ ] Assign Player Prefab: drag HolePlayer prefab
- [ ] Add ObjectSpawner component
  - [ ] Assign Consumable Prefabs array
  - [ ] Add consumable prefabs to array
  - [ ] Set Max Objects: 50
  - [ ] Set Spawn Interval: 2

### Configure Camera
- [ ] Select Main Camera
- [ ] Add CameraFollow component
- [ ] Position: (0, 15, -10)
- [ ] Rotation: (45, 0, 0)

### Setup UI (Basic)
- [ ] GameObject > UI > Canvas (creates Canvas + EventSystem)
- [ ] On Canvas, add GameUI component
- [ ] Create UI structure (optional for now):
  - [ ] Menu Panel with Host/Join buttons
  - [ ] Game Panel with Score/Size text
  - [ ] Assign references in GameUI component

### Save Scene
- [ ] File > Save (or Ctrl+S)

---

## ☐ Build Configuration

### Build Settings
- [ ] File > Build Settings
- [ ] Verify GameScene is in "Scenes In Build"
- [ ] If not, click "Add Open Scenes"

### Android Setup (if building for Android)
- [ ] Click "Android" platform
- [ ] Click "Switch Platform" (wait for reimport)
- [ ] Player Settings button
  - [ ] Company Name: (your choice)
  - [ ] Product Name: CornHole
  - [ ] Package Name: com.yourcompany.cornhole
  - [ ] Minimum API Level: 22 or higher
  - [ ] Scripting Backend: IL2CPP
  - [ ] Target Architectures: ARM64 + ARMv7

### iOS Setup (if building for iOS)
- [ ] Click "iOS" platform
- [ ] Click "Switch Platform"
- [ ] Player Settings:
  - [ ] Bundle Identifier: com.yourcompany.cornhole
  - [ ] Target minimum iOS Version: 11.0
  - [ ] Architecture: ARM64

---

## ☐ Testing

### Desktop Test (Standalone Build)
- [ ] File > Build Settings
- [ ] Select Standalone (Windows/Mac/Linux)
- [ ] Click "Build and Run"
- [ ] Choose save location
- [ ] Wait for build (2-5 minutes)
- [ ] **In built game**: Click "Host Game" button
- [ ] **In Unity Editor**: Press Play
- [ ] **In Editor**: Click "Join Game" button

### Expected Results
- [ ] Both instances connect
- [ ] You see two holes in the game
- [ ] Each can control their own hole
- [ ] Objects fall from the sky
- [ ] Holes can consume objects (if small enough)
- [ ] Hole grows when eating objects
- [ ] Score increases
- [ ] Both players see synchronized state

### Mobile Test (Android)
- [ ] Connect Android device via USB
- [ ] Enable USB Debugging on device
- [ ] File > Build Settings > Android
- [ ] Click "Build and Run"
- [ ] App installs and launches
- [ ] Test touch controls work
- [ ] Can connect to game hosted on PC/Editor

### Mobile Test (iOS)
- [ ] Requires macOS and Xcode
- [ ] File > Build Settings > iOS
- [ ] Click Build
- [ ] Open generated Xcode project
- [ ] Configure signing
- [ ] Build and deploy to device
- [ ] Test on device

---

## ☐ Troubleshooting

If something doesn't work:
- [ ] Check Console for errors (red messages)
- [ ] Verify all prefabs have NetworkObject component
- [ ] Confirm App ID is set correctly
- [ ] Check internet connection
- [ ] See TROUBLESHOOTING.md for specific issues
- [ ] Restart Unity if needed

---

## ☐ Polish & Enhancement (Optional)

### Visual Polish
- [ ] Create better materials for hole and objects
- [ ] Add particle effects for consumption
- [ ] Add skybox for better environment
- [ ] Improve lighting

### Gameplay Enhancement
- [ ] Add more object types
- [ ] Implement player collision
- [ ] Add sound effects
- [ ] Add background music
- [ ] Create better UI

### Mobile Polish
- [ ] Add virtual joystick (alternative to direct touch)
- [ ] Optimize for different screen sizes
- [ ] Add haptic feedback
- [ ] Implement quality settings menu

---

## ☐ Deployment

### Android
- [ ] Generate signing key
- [ ] Configure keystore in Player Settings
- [ ] Build > Build App Bundle (AAB)
- [ ] Upload to Google Play Console
- [ ] Test on various devices

### iOS
- [ ] Set up App Store Connect
- [ ] Configure certificates and provisioning profiles
- [ ] Archive build in Xcode
- [ ] Upload to App Store Connect
- [ ] Submit for review

---

## Completion Status

**Basic Setup Complete When**:
- ✓ Project opens without errors
- ✓ Photon Fusion installed
- ✓ App ID configured
- ✓ All prefabs created
- ✓ Scene set up
- ✓ Can test multiplayer locally

**Ready for Release When**:
- ✓ All basic features working
- ✓ Mobile build successful
- ✓ Touch controls working
- ✓ Performance acceptable
- ✓ No critical bugs
- ✓ Visual polish complete
- ✓ Tested on multiple devices

---

## Time Estimates

| Task | Time |
|------|------|
| Initial setup | 10 min |
| Photon installation | 5 min |
| Photon configuration | 5 min |
| Create prefabs | 15-20 min |
| Scene setup | 10-15 min |
| First test | 5 min |
| Visual polish | 30-60 min |
| Mobile build | 10-15 min |
| **Total (Basic)** | **~60-75 min** |
| **Total (Polished)** | **~90-135 min** |

---

## Need Help?

- 📖 See QUICKSTART.md for detailed steps
- 🔧 See TROUBLESHOOTING.md for common issues
- 📚 See DEVELOPMENT.md for code explanations
- 🏗️ See ARCHITECTURE.md for system design
- 🎨 See VISUAL_ASSETS.md for art creation

**Good luck! 🎮**
