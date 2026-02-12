# Photon Fusion Setup Guide

This guide will help you set up Photon Fusion for the Corn Hole multiplayer game.

## Step 1: Get Photon Fusion

There are two ways to install Photon Fusion:

### Option A: Unity Asset Store (Recommended)

1. Open Unity Asset Store in your browser
2. Search for "Photon Fusion"
3. Add it to your assets
4. In Unity, open Package Manager (Window > Package Manager)
5. Select "My Assets" from the dropdown
6. Find "Photon Fusion" and click Download/Import
7. Import all files

### Option B: Manual Download

1. Visit [Photon Engine Downloads](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)
2. Download Photon Fusion SDK
3. Import the .unitypackage file into your Unity project

## Step 2: Create Photon Account and App

1. Go to [Photon Dashboard](https://dashboard.photonengine.com/)
2. Sign up or log in
3. Click "Create a New App"
4. Select "Photon Fusion" as the type
5. Give it a name (e.g., "CornHole")
6. Copy the App ID

## Step 3: Configure Fusion in Unity

1. In Unity, go to `Fusion > Realtime Settings` (or similar menu)
2. Paste your App ID in the appropriate field
3. Save the settings

Alternatively, you can create a PhotonAppSettings file:

1. Right-click in Project window
2. Create > Photon > App Settings
3. Paste your App ID
4. Select the appropriate region (e.g., "us", "eu", "asia")

## Step 4: Verify Installation

1. Check that `Fusion.Runtime.dll` is in your project
2. Verify scripts compile without errors
3. Look for Fusion menus in Unity's top menu bar

## Step 5: Network Prefab Setup

1. Create NetworkPrefabSource:
   - Create > Fusion > Network Prefab Source
   - Add all your networked prefabs (HolePlayer, ConsumableObjects)

2. Configure NetworkProjectConfig:
   - Fusion > Network Project Config
   - Assign the Network Prefab Source
   - Configure simulation settings if needed

## Troubleshooting

### "Fusion namespace not found"
- Ensure Fusion is properly imported
- Check that assembly references are correct in .asmdef files
- Restart Unity

### "App ID not set"
- Double-check the App ID in Photon settings
- Make sure PhotonAppSettings is in Resources folder

### "Connection failed"
- Verify internet connection
- Check firewall settings
- Ensure App ID is valid
- Try different region in settings

## Testing Multiplayer

To test multiplayer locally:

1. Build the game
2. Run the built version
3. Press Play in Unity Editor
4. One should host, the other should join
5. They should connect and see each other

## Mobile Build Considerations

### Android
- Ensure INTERNET permission is set in Player Settings
- IL2CPP scripting backend recommended
- Target API 22+

### iOS
- Set minimum iOS version to 11.0+
- Add required capabilities if needed
- Test on actual device (not just simulator)

## Network Configuration

Default Fusion settings should work, but you can tune:

- **Tick Rate**: 60 Hz (default) - balance between smoothness and bandwidth
- **Snapshot Interval**: Affects state synchronization frequency
- **Lag Compensation**: Enabled for better player experience

## Next Steps

After setup:

1. Create player prefab with NetworkObject + NetworkTransform + HolePlayer
2. Create consumable prefabs with NetworkObject + ConsumableObject
3. Set up NetworkManager in the scene
4. Test locally first, then with multiple devices

## Resources

- [Photon Fusion Documentation](https://doc.photonengine.com/fusion/current/getting-started/fusion-intro)
- [Fusion API Reference](https://doc-api.photonengine.com/en/fusion/current/)
- [Community Forum](https://forum.photonengine.com/)
- [Discord](https://discord.gg/photonengine)
