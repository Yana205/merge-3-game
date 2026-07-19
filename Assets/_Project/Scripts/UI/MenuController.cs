using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the overlay main menu and the game-over panel buttons for the endless
/// run. Play and Replay both start a fresh infinite run; there is no level
/// select any more. All wiring happens in code (Awake) so the scene only needs
/// object references, no serialized UnityEvents.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Panels (assign in Inspector)")]
    [SerializeField] private GameObject _menuPanel;

    [Header("References (assign in Inspector)")]
    [SerializeField] private LevelSelectUI _levelSelect;
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private LevelManager _levelManager;
    [SerializeField] private ScreenFader _fader;
    [SerializeField] private ProgressManager _progressManager;

    [Header("Menu Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _leaderboardButton;
    [SerializeField] private LeaderboardUI _leaderboard;

    [Tooltip("Optional label that shows the best run score on the menu.")]
    [SerializeField] private TMPro.TMP_Text _bestScoreText;

    [Header("Game Over Buttons")]
    [SerializeField] private Button _replayButton;
    [SerializeField] private Button _gameOverMenuButton;

    // Starting a run hides the menu; this static survives an in-place restart so
    // the menu only auto-shows on the first load of a play session.
    private static bool s_menuDismissed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_menuDismissed = false;
    }

    void Awake()
    {
        if (_menuPanel == null)
        {
            Debug.LogError("MenuController: menu panel reference is missing.");
            return;
        }

        if (_playButton != null) _playButton.onClick.AddListener(StartRun);
        if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
        if (_leaderboardButton != null) _leaderboardButton.onClick.AddListener(OpenLeaderboard);
        if (_replayButton != null) _replayButton.onClick.AddListener(StartRun);
        if (_gameOverMenuButton != null) _gameOverMenuButton.onClick.AddListener(ReturnToMenu);

        RefreshBestScore();
        _menuPanel.SetActive(!s_menuDismissed);
        if (_uiManager != null) _uiManager.SetHudVisible(s_menuDismissed);
    }

    void RefreshBestScore()
    {
        if (_bestScoreText == null) return;
        int best = _progressManager != null ? _progressManager.GetBestScore() : 0;
        _bestScoreText.text = best > 0 ? "Best  " + best : "";
    }

    // Play and Replay both kick off a brand-new infinite run.
    public void StartRun()
    {
        if (_fader != null)
            _fader.RunTransition(BeginRun);
        else
            BeginRun();
    }

    // Midpoint of the fade: swap the menu for a fresh run, in place.
    void BeginRun()
    {
        s_menuDismissed = true;
        _menuPanel.SetActive(false);

        if (_uiManager != null)
        {
            _uiManager.HideGameOver();
            _uiManager.SetHudVisible(true);
        }

        if (_levelManager != null)
            _levelManager.StartEndlessRun();
    }

    public void ReturnToMenu()
    {
        if (_uiManager != null)
        {
            _uiManager.HideGameOver();
            _uiManager.SetHudVisible(false);
        }
        s_menuDismissed = false;
        RefreshBestScore();
        _menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        if (_levelSelect != null)
            _levelSelect.QuitGame();
        else
            Application.Quit();
    }

    public void OpenLeaderboard()
    {
        if (_leaderboard != null)
            _leaderboard.Show();
    }

    // For flows that need the menu on the next scene load rather than a run.
    public static void ShowMenuOnNextLoad()
    {
        s_menuDismissed = false;
    }
}
