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
    [Tooltip("The UI Toolkit gameplay HUD. UIManager.SetHudVisible only reaches " +
             "the uGUI text, so the UIDocument is toggled separately.")]
    [SerializeField] private GameObject _gameplayHud;

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

    [Header("Audio")]
    [Tooltip("Title-screen MUSIC toggle. Music starts muted, so the player opts in here.")]
    [SerializeField] private Button _musicButton;

    [Tooltip("The MUSIC entry's label; found on the button's children when unset.")]
    [SerializeField] private TMPro.TMP_Text _musicLabel;

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

        // The UI Toolkit HUD lives on its own GameObject. If the scene link is
        // ever lost (it was, once, in a rewrite) the HUD would sit on top of the
        // title screen, so fall back to finding it rather than trusting the link.
        if (_gameplayHud == null)
        {
            var hud = FindAnyObjectByType<UIController>(FindObjectsInactive.Include);
            if (hud != null) _gameplayHud = hud.gameObject;
            else Debug.LogError("MenuController: gameplay HUD reference is missing and no UIController was found.");
        }

        if (_playButton != null) _playButton.onClick.AddListener(StartRun);
        if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
        if (_leaderboardButton != null) _leaderboardButton.onClick.AddListener(OpenLeaderboard);
        if (_replayButton != null) _replayButton.onClick.AddListener(StartRun);
        if (_gameOverMenuButton != null) _gameOverMenuButton.onClick.AddListener(ReturnToMenu);

        if (_musicButton != null)
        {
            if (_musicLabel == null) _musicLabel = _musicButton.GetComponentInChildren<TMPro.TMP_Text>();
            _musicButton.onClick.AddListener(AudioDirector.ToggleMusic);
        }
        AudioDirector.MuteChanged += RefreshMusicLabel;
        RefreshMusicLabel();

        RefreshBestScore();
        _menuPanel.SetActive(!s_menuDismissed);
        SetHudVisible(s_menuDismissed);

        // The HUD's RESTART reaches us over the bus rather than through a
        // reference, so the gameplay HUD stays ignorant of the menu.
        GameEvents.RestartRequested += StartRun;
    }

    void OnDestroy()
    {
        GameEvents.RestartRequested -= StartRun;
        AudioDirector.MuteChanged -= RefreshMusicLabel;
    }

    // Mirrors the HUD's MUSIC button: both read the same static state, so a
    // toggle on either screen shows up on the other.
    void RefreshMusicLabel()
    {
        if (_musicLabel != null)
            _musicLabel.text = AudioDirector.MusicMuted ? "MUSIC: OFF" : "MUSIC: ON";
    }

    // The HUD lives in two systems: uGUI score/target text behind UIManager, and
    // the UI Toolkit UIDocument. Both have to move together or the banner shows
    // with a score panel floating over it.
    void SetHudVisible(bool visible)
    {
        if (_uiManager != null) _uiManager.SetHudVisible(visible);
        if (_gameplayHud != null) _gameplayHud.SetActive(visible);
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
            _uiManager.HideGameOver();
        SetHudVisible(true);

        if (_levelManager != null)
            _levelManager.StartEndlessRun();
    }

    public void ReturnToMenu()
    {
        if (_uiManager != null)
            _uiManager.HideGameOver();
        SetHudVisible(false);

        s_menuDismissed = false;
        RefreshBestScore();
        _menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
