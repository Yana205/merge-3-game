using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the overlay main menu and the game-over panel buttons.
/// All button wiring happens in code (Awake) so the scene only needs
/// object references, no serialized UnityEvents.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Panels (assign in Inspector)")]
    [SerializeField] private GameObject _menuPanel;

    [Header("References (assign in Inspector)")]
    [SerializeField] private LevelSelectUI _levelSelect;
    [SerializeField] private UIManager _uiManager;

    [Header("Menu Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _level1Button;
    [SerializeField] private Button _level2Button;
    [SerializeField] private Button _quitButton;

    [Header("Game Over Buttons")]
    [SerializeField] private Button _replayButton;
    [SerializeField] private Button _gameOverMenuButton;

    // Starting a level reloads the scene, which would re-show the menu.
    // This static survives the reload (but not a new play session), so the
    // menu is only shown on the first load of a session.
    private static bool s_menuDismissed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_menuDismissed = false;
    }

    void Awake()
    {
        if (_menuPanel == null || _levelSelect == null)
        {
            Debug.LogError("MenuController: menu panel or LevelSelectUI reference is missing.");
            return;
        }

        if (_playButton != null) _playButton.onClick.AddListener(() => StartLevel(0));
        if (_level1Button != null) _level1Button.onClick.AddListener(() => StartLevel(0));
        if (_level2Button != null) _level2Button.onClick.AddListener(() => StartLevel(1));
        if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
        if (_replayButton != null) _replayButton.onClick.AddListener(ReplayLevel);
        if (_gameOverMenuButton != null) _gameOverMenuButton.onClick.AddListener(ReturnToMenu);

        _menuPanel.SetActive(!s_menuDismissed);
    }

    public void StartLevel(int levelIndex)
    {
        s_menuDismissed = true;
        _menuPanel.SetActive(false);
        _levelSelect.LoadLevel(levelIndex);
    }

    public void ReplayLevel()
    {
        StartLevel(PlayerPrefs.GetInt("SelectedLevel", 0));
    }

    public void ReturnToMenu()
    {
        if (_uiManager != null)
            _uiManager.HideGameOver();
        s_menuDismissed = false;
        _menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        _levelSelect.QuitGame();
    }
}
