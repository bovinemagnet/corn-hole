using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CornHole
{
    /// <summary>
    /// Simple UI manager for the game
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI sizeText;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject gamePanel;

        private NetworkManager networkManager;
        private HolePlayer localPlayer;

        private void Start()
        {
            networkManager = FindObjectOfType<NetworkManager>();
            
            if (hostButton != null)
            {
                hostButton.onClick.AddListener(OnHostGame);
            }

            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinGame);
            }

            ShowMenu();
        }

        private void Update()
        {
            if (localPlayer != null)
            {
                UpdatePlayerStats();
            }
            else
            {
                FindLocalPlayer();
            }
        }

        private void FindLocalPlayer()
        {
            HolePlayer[] players = FindObjectsOfType<HolePlayer>();
            foreach (var player in players)
            {
                if (player.Object.HasInputAuthority)
                {
                    localPlayer = player;
                    break;
                }
            }
        }

        private void UpdatePlayerStats()
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {localPlayer.Score}";
            }

            if (sizeText != null)
            {
                sizeText.text = $"Size: {localPlayer.HoleRadius:F1}";
            }
        }

        private void OnHostGame()
        {
            if (networkManager != null)
            {
                networkManager.StartGame(Fusion.GameMode.Host);
                ShowGame();
            }
        }

        private void OnJoinGame()
        {
            if (networkManager != null)
            {
                networkManager.StartGame(Fusion.GameMode.Client);
                ShowGame();
            }
        }

        private void ShowMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(true);
            if (gamePanel != null) gamePanel.SetActive(false);
        }

        private void ShowGame()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (gamePanel != null) gamePanel.SetActive(true);
        }
    }
}
