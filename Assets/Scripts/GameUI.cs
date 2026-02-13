using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace CornHole
{
    /// <summary>
    /// UI manager handling menu, join code entry, lobby, HUD, and end screen.
    /// Flow: Menu → (Host) Lobby | Menu → Join Panel → Lobby → Game → End
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Menu Panel")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;

        [Header("Join Panel")]
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button submitJoinButton;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private TextMeshProUGUI joinErrorText;

        [Header("Lobby Panel")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private TextMeshProUGUI joinCodeDisplay;
        [SerializeField] private TextMeshProUGUI playerListText;
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonLabel;
        [SerializeField] private Button startMatchButton;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("Game Panel (HUD)")]
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI sizeText;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("End Screen")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI finalSizeText;
        [SerializeField] private Button returnToMenuButton;

        private NetworkManager _networkManager;
        private HolePlayer _localPlayer;
        private MatchTimer _matchTimer;
        private bool _isReady;

        private void Start()
        {
            _networkManager = FindAnyObjectByType<NetworkManager>();

            // Menu buttons
            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostGame);
            if (joinButton != null)
                joinButton.onClick.AddListener(OnShowJoinPanel);

            // Join panel buttons
            if (submitJoinButton != null)
                submitJoinButton.onClick.AddListener(OnSubmitJoinCode);
            if (backToMenuButton != null)
                backToMenuButton.onClick.AddListener(ShowMenu);

            // Lobby buttons
            if (readyButton != null)
                readyButton.onClick.AddListener(OnToggleReady);
            if (startMatchButton != null)
                startMatchButton.onClick.AddListener(OnStartMatch);

            // End screen
            if (returnToMenuButton != null)
                returnToMenuButton.onClick.AddListener(OnReturnToMenu);

            // Subscribe to network events
            if (_networkManager != null)
            {
                _networkManager.OnConnectionError += ShowJoinError;
                _networkManager.OnSessionJoined += OnSessionJoined;
            }

            ShowMenu();
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnConnectionError -= ShowJoinError;
                _networkManager.OnSessionJoined -= OnSessionJoined;
            }
        }

        private void Update()
        {
            // Lazily find MatchTimer
            if (_matchTimer == null)
            {
                _matchTimer = FindAnyObjectByType<MatchTimer>();
            }

            // Lazily find local player
            if (_localPlayer == null)
            {
                FindLocalPlayer();
            }

            // Update UI based on current match phase
            if (_matchTimer != null)
            {
                if (_matchTimer.IsLobby)
                {
                    UpdateLobbyDisplay();
                }
                else if (_matchTimer.IsCountdown)
                {
                    UpdateCountdownDisplay();
                }
                else if (_matchTimer.IsPlaying)
                {
                    EnsureGamePanel();
                    UpdatePlayerStats();
                    UpdateTimerDisplay();
                }
                else if (_matchTimer.HasEnded)
                {
                    ShowEndScreen();
                }
            }
        }

        private void FindLocalPlayer()
        {
            HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player.Object != null && player.Object.HasInputAuthority)
                {
                    _localPlayer = player;
                    break;
                }
            }
        }

        // --- Menu ---

        private void OnHostGame()
        {
            if (_networkManager != null)
            {
                _networkManager.StartHost();
                // Will transition to lobby via OnSessionJoined callback
            }
        }

        private void OnShowJoinPanel()
        {
            SetAllPanels(false);
            if (joinPanel != null) joinPanel.SetActive(true);
            if (joinErrorText != null) joinErrorText.text = "";
            if (joinCodeInput != null) joinCodeInput.text = "";
        }

        private void OnSubmitJoinCode()
        {
            if (_networkManager == null || joinCodeInput == null)
                return;

            string code = joinCodeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                ShowJoinError("Please enter a join code.");
                return;
            }

            _networkManager.JoinGame(code);
        }

        private void ShowJoinError(string message)
        {
            if (joinErrorText != null)
            {
                joinErrorText.text = message;
            }
        }

        private void OnSessionJoined()
        {
            ShowLobby();
        }

        // --- Lobby ---

        private void ShowLobby()
        {
            SetAllPanels(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);

            // Display join code
            if (joinCodeDisplay != null && _networkManager != null)
            {
                joinCodeDisplay.text = $"Join Code: {_networkManager.JoinCode}";
            }

            // Only host sees the start button
            if (startMatchButton != null)
            {
                startMatchButton.gameObject.SetActive(
                    _networkManager != null && _networkManager.IsHost);
            }

            // Reset ready state
            _isReady = false;
            UpdateReadyButtonLabel();

            // Hide countdown text initially
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
            }
        }

        private void UpdateLobbyDisplay()
        {
            // Update player list
            if (playerListText != null)
            {
                HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
                string list = "";
                foreach (var player in players)
                {
                    if (player.Object == null) continue;
                    string name = player.PlayerName.ToString();
                    if (string.IsNullOrEmpty(name)) name = $"Player {player.Object.InputAuthority.PlayerId}";
                    string readyMark = player.IsReady ? " [Ready]" : "";
                    list += $"{name}{readyMark}\n";
                }
                playerListText.text = list;
            }

            // Enable start button only when at least one player is ready
            if (startMatchButton != null && _networkManager != null && _networkManager.IsHost)
            {
                bool anyReady = false;
                HolePlayer[] players = FindObjectsByType<HolePlayer>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    if (player.IsReady) { anyReady = true; break; }
                }
                startMatchButton.interactable = anyReady;
            }
        }

        private void OnToggleReady()
        {
            if (_localPlayer == null) return;

            _isReady = !_isReady;
            _localPlayer.RPC_SetReady(_isReady);
            UpdateReadyButtonLabel();
        }

        private void UpdateReadyButtonLabel()
        {
            if (readyButtonLabel != null)
            {
                readyButtonLabel.text = _isReady ? "Not Ready" : "Ready";
            }
        }

        private void OnStartMatch()
        {
            if (_networkManager == null || !_networkManager.IsHost)
                return;

            if (_networkManager.MatchTimer != null)
            {
                _networkManager.MatchTimer.StartCountdown();
            }
        }

        // --- Countdown ---

        private void UpdateCountdownDisplay()
        {
            // Show countdown overlay on lobby
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                int seconds = Mathf.CeilToInt(_matchTimer.RemainingTime);
                countdownText.text = seconds > 0 ? seconds.ToString() : "GO!";
            }

            // Disable lobby buttons during countdown
            if (readyButton != null) readyButton.interactable = false;
            if (startMatchButton != null) startMatchButton.interactable = false;
        }

        // --- Game HUD ---

        private bool _gamePanelShown;

        private void EnsureGamePanel()
        {
            if (!_gamePanelShown)
            {
                SetAllPanels(false);
                if (gamePanel != null) gamePanel.SetActive(true);
                _gamePanelShown = true;
            }
        }

        private void UpdatePlayerStats()
        {
            if (_localPlayer == null) return;

            if (scoreText != null)
                scoreText.text = $"Score: {_localPlayer.Score}";

            if (sizeText != null)
                sizeText.text = $"Size: {_localPlayer.HoleRadius:F1}";
        }

        private void UpdateTimerDisplay()
        {
            if (timerText == null || _matchTimer == null) return;

            float remaining = _matchTimer.RemainingTime;
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        // --- End Screen ---

        private bool _endScreenShown;

        private void ShowEndScreen()
        {
            if (_endScreenShown) return;
            _endScreenShown = true;

            SetAllPanels(false);
            if (endPanel != null) endPanel.SetActive(true);

            if (finalScoreText != null && _localPlayer != null)
                finalScoreText.text = $"Final Score: {_localPlayer.Score}";

            if (finalSizeText != null && _localPlayer != null)
                finalSizeText.text = $"Final Size: {_localPlayer.HoleRadius:F1}";
        }

        // --- Navigation ---

        private void OnReturnToMenu()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ShowMenu()
        {
            SetAllPanels(false);
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        private void SetAllPanels(bool active)
        {
            if (menuPanel != null) menuPanel.SetActive(active);
            if (joinPanel != null) joinPanel.SetActive(active);
            if (lobbyPanel != null) lobbyPanel.SetActive(active);
            if (gamePanel != null) gamePanel.SetActive(active);
            if (endPanel != null) endPanel.SetActive(active);
        }
    }
}
