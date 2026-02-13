using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;

namespace CornHole
{
    /// <summary>
    /// Main network manager for the game
    /// Handles Photon Fusion connection and player spawning
    /// Uses Host Mode (one player is server + player)
    /// </summary>
    public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Network Settings")]
        [SerializeField] private NetworkPrefabRef playerPrefab;
        [SerializeField] private NetworkPrefabRef matchTimerPrefab;
        [SerializeField] private string gameVersion = "1.0";
        [SerializeField] private int maxPlayers = 8;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3[] spawnPoints = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 10),
            new Vector3(-10, 0, 10),
            new Vector3(10, 0, -10),
            new Vector3(-10, 0, -10),
            new Vector3(0, 0, 15),
            new Vector3(0, 0, -15),
            new Vector3(15, 0, 0)
        };

        private NetworkRunner _runner;
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
        private bool _matchTimerSpawned;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public async void StartGame(GameMode mode)
        {
            // Create the NetworkRunner
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            // Start the game
            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = mode,
                SessionName = "CornHoleGame",
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (result.Ok)
            {
                Debug.Log($"Game started successfully in {mode} mode");
            }
            else
            {
                Debug.LogError($"Failed to start game: {result.ShutdownReason}");
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                Debug.Log($"Player {player} joined");

                // Spawn match timer once (first player triggers it)
                if (!_matchTimerSpawned)
                {
                    runner.Spawn(matchTimerPrefab, Vector3.zero, Quaternion.identity);
                    _matchTimerSpawned = true;
                }

                // Spawn player at a spawn point
                Vector3 spawnPosition = GetSpawnPoint(player);
                NetworkObject networkPlayerObject = runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);
                _spawnedPlayers[player] = networkPlayerObject;
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_spawnedPlayers.TryGetValue(player, out NetworkObject networkObject))
            {
                runner.Despawn(networkObject);
                _spawnedPlayers.Remove(player);
            }

            Debug.Log($"Player {player} left");
        }

        private Vector3 GetSpawnPoint(PlayerRef player)
        {
            int index = player.PlayerId % spawnPoints.Length;
            return spawnPoints[index];
        }

        #region INetworkRunnerCallbacks Implementation

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("Connected to server");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"Disconnected from server: {reason}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"Connect failed: {reason}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (_spawnedPlayers.Count < maxPlayers)
            {
                request.Accept();
            }
            else
            {
                request.Refuse();
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // Input is handled directly in HolePlayer for now
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"Shutdown: {shutdownReason}");
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            Debug.Log("Host migration started");
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        #endregion
    }
}
