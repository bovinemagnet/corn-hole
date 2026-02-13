// Minimal Fusion stub — allows compilation without the Photon Fusion SDK.
// Replace this entire folder with the real SDK when ready for networking.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Fusion
{
    // Core enums
    public enum GameMode { Host, Client, Server, Single, Shared, AutoHostOrClient }
    public enum ShutdownReason { Ok, Error, GameClosed, IncompatibleConfiguration, ServerInRoom, DisconnectedByPluginLogic, GameNotFound, MaxCcuReached, CustomAuthenticationFailed, GameIdAlreadyExists, OperationTimeout, OperationCanceled, ConnectionRefused, ConnectionTimeout, AlreadyRunning, InvalidArguments }
    public enum NetDisconnectReason { Default, Timeout, Error, Requested }
    public enum NetConnectFailedReason { Default, Timeout, Refused, ServerFull }
    public enum RpcSources { StateAuthority = 1, InputAuthority = 2, Proxies = 4, All = 7 }
    public enum RpcTargets { StateAuthority = 1, InputAuthority = 2, Proxies = 4, All = 7 }

    // Attributes
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class NetworkedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public RpcAttribute(RpcSources sources, RpcTargets targets) { }
    }

    // Structs
    public struct PlayerRef
    {
        public int PlayerId;
        public static bool operator ==(PlayerRef a, PlayerRef b) => a.PlayerId == b.PlayerId;
        public static bool operator !=(PlayerRef a, PlayerRef b) => a.PlayerId != b.PlayerId;
        public override bool Equals(object obj) => obj is PlayerRef p && p.PlayerId == PlayerId;
        public override int GetHashCode() => PlayerId;
        public override string ToString() => PlayerId.ToString();
    }

    public struct NetworkPrefabRef { }

    public struct TickTimer
    {
        private float _target;
        private bool _active;
        public static TickTimer CreateFromSeconds(NetworkRunner runner, float seconds)
        {
            return new TickTimer { _target = Time.time + seconds, _active = true };
        }
        public bool Expired(NetworkRunner runner) => _active && Time.time >= _target;
        public bool ExpiredOrNotRunning(NetworkRunner runner) => !_active || Time.time >= _target;
    }

    public struct NetAddress
    {
        public override string ToString() => "localhost";
    }

    public struct ReliableKey { }
    public struct HostMigrationToken { }
    public struct SimulationMessagePtr { }

    public class SessionInfo
    {
        public string Name;
        public int PlayerCount;
        public int MaxPlayers;
    }

    public struct StartGameArgs
    {
        public GameMode GameMode;
        public string SessionName;
        public int Scene;
        public NetworkSceneManagerDefault SceneManager;
    }

    public struct StartGameResult
    {
        public bool Ok;
        public ShutdownReason ShutdownReason;
    }

    // NetworkObject stub
    public class NetworkObject : MonoBehaviour
    {
        public bool HasStateAuthority => true;
        public bool HasInputAuthority => true;
    }

    // NetworkRunner stub
    public class NetworkRunner : MonoBehaviour
    {
        public bool IsServer => true;
        public bool ProvideInput { get; set; }
        public float DeltaTime => Time.fixedDeltaTime;

        public Task<StartGameResult> StartGame(StartGameArgs args)
        {
            return Task.FromResult(new StartGameResult { Ok = true });
        }

        public NetworkObject Spawn(NetworkPrefabRef prefab, Vector3 position, Quaternion rotation, PlayerRef? inputAuthority = null)
        {
            Debug.LogWarning("[FusionStub] Spawn(NetworkPrefabRef) is a no-op stub.");
            return null;
        }

        public void Despawn(NetworkObject obj)
        {
            if (obj != null) Destroy(obj.gameObject);
        }
    }

    // NetworkBehaviour stub
    public class NetworkBehaviour : MonoBehaviour
    {
        public new NetworkObject Object => GetComponent<NetworkObject>();
        public NetworkRunner Runner => FindAnyObjectByType<NetworkRunner>();

        public virtual void Spawned() { }
        public virtual void FixedUpdateNetwork() { }

        private void Start() { Spawned(); }
        private void FixedUpdate() { FixedUpdateNetwork(); }
    }

    public class NetworkSceneManagerDefault : MonoBehaviour { }
}

namespace Fusion.Sockets
{
    public interface INetworkRunnerCallbacks
    {
        void OnPlayerJoined(Fusion.NetworkRunner runner, Fusion.PlayerRef player);
        void OnPlayerLeft(Fusion.NetworkRunner runner, Fusion.PlayerRef player);
        void OnInput(Fusion.NetworkRunner runner, Fusion.NetworkInput input);
        void OnInputMissing(Fusion.NetworkRunner runner, Fusion.PlayerRef player, Fusion.NetworkInput input);
        void OnShutdown(Fusion.NetworkRunner runner, Fusion.ShutdownReason shutdownReason);
        void OnConnectedToServer(Fusion.NetworkRunner runner);
        void OnDisconnectedFromServer(Fusion.NetworkRunner runner, Fusion.NetDisconnectReason reason);
        void OnConnectRequest(Fusion.NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token);
        void OnConnectFailed(Fusion.NetworkRunner runner, Fusion.NetAddress remoteAddress, Fusion.NetConnectFailedReason reason);
        void OnUserSimulationMessage(Fusion.NetworkRunner runner, Fusion.SimulationMessagePtr message);
        void OnSessionListUpdated(Fusion.NetworkRunner runner, System.Collections.Generic.List<Fusion.SessionInfo> sessionList);
        void OnCustomAuthenticationResponse(Fusion.NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data);
        void OnHostMigration(Fusion.NetworkRunner runner, Fusion.HostMigrationToken hostMigrationToken);
        void OnReliableDataReceived(Fusion.NetworkRunner runner, Fusion.PlayerRef player, Fusion.ReliableKey key, System.ArraySegment<byte> data);
        void OnReliableDataProgress(Fusion.NetworkRunner runner, Fusion.PlayerRef player, Fusion.ReliableKey key, float progress);
        void OnSceneLoadDone(Fusion.NetworkRunner runner);
        void OnSceneLoadStart(Fusion.NetworkRunner runner);
        void OnObjectExitAOI(Fusion.NetworkRunner runner, Fusion.NetworkObject obj, Fusion.PlayerRef player);
        void OnObjectEnterAOI(Fusion.NetworkRunner runner, Fusion.NetworkObject obj, Fusion.PlayerRef player);
    }

    public class NetworkRunnerCallbackArgs
    {
        public class ConnectRequest
        {
            public void Accept() { }
            public void Refuse() { }
        }
    }
}

namespace Fusion
{
    // NetworkInput needs to be in the Fusion namespace (used by INetworkRunnerCallbacks)
    public struct NetworkInput { }
}
