using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace CornHole
{
    /// <summary>
    /// UI manager handling menus, HUD, timer, and end screen.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI sizeText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject gamePanel;

        [Header("End Screen")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI finalSizeText;
        [SerializeField] private Button returnToMenuButton;

        private NetworkManager networkManager;
        private HolePlayer localPlayer;
        private MatchTimer matchTimer;

        private void Start()
        {
            networkManager = FindAnyObjectByType<NetworkManager>();

            if (hostButton != null)
            {
                hostButton.onClick.AddListener(OnHostGame);
            }

            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinGame);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.AddListener(OnReturnToMenu);
            }

            if (endPanel != null)
            {
                endPanel.SetActive(false);
            }

            ShowMenu();
        }

        private void Update()
        {
            if (matchTimer == null)
            {
                matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            if (localPlayer != null)
            {
                UpdatePlayerStats();
                UpdateTimerDisplay();
                CheckMatchEnd();
            }
            else
            {
                FindLocalPlayer();
            }
        }

        private void FindLocalPlayer()
        {
            HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player.Object != null && player.Object.HasInputAuthority)
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

        private void UpdateTimerDisplay()
        {
            if (timerText == null || matchTimer == null)
                return;

            float remaining = matchTimer.RemainingTime;
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private void CheckMatchEnd()
        {
            if (matchTimer == null || !matchTimer.HasEnded)
                return;

            if (endPanel != null && !endPanel.activeSelf)
            {
                ShowEndScreen();
            }
        }

        private void ShowEndScreen()
        {
            if (endPanel != null)
            {
                endPanel.SetActive(true);
            }

            if (gamePanel != null)
            {
                gamePanel.SetActive(false);
            }

            if (finalScoreText != null && localPlayer != null)
            {
                finalScoreText.text = $"Final Score: {localPlayer.Score}";
            }

            if (finalSizeText != null && localPlayer != null)
            {
                finalSizeText.text = $"Final Size: {localPlayer.HoleRadius:F1}";
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

        private void OnReturnToMenu()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
