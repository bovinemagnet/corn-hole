Fusion Stub — Temporary Compilation Shim
=========================================

This folder contains a minimal stub that satisfies the Fusion.Runtime
assembly reference so the project compiles without the Photon Fusion SDK.

Networking features are non-functional. NetworkBehaviour.Spawned() and
FixedUpdateNetwork() are routed through MonoBehaviour Start/FixedUpdate
so game logic runs in single-player mode.

Replacing with the Real Photon Fusion SDK
------------------------------------------

1. Delete this entire folder:
     Assets/Plugins/FusionStub/

2. Import Photon Fusion 2 via one of:
   - Unity Package Manager > Add package from git URL
   - Assets > Import Package > Custom Package (.unitypackage from dashboard.photonengine.com)

3. Configure your Photon App ID:
   - Create an app at https://dashboard.photonengine.com
   - In Unity: Fusion > Setup > paste your App ID

4. Verify the project compiles with no errors in the Console window.

The CornHole.Scripts.asmdef already references "Fusion.Runtime", which
is the assembly name used by the real Photon Fusion SDK — no further
changes are needed.
