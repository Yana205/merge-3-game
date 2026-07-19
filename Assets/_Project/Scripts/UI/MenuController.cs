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
    [SerializeField] private LevelManager _levelManager;
    [SerializeField] private ScreenFader _fader;
    [SerializeField] private ProgressManager _progressManager;

    [Header("Menu Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _leaderboardButton;
    [SerializeField] private LeaderboardUI _leaderboard;

    [Header("Level Buttons (generated from LevelManager.levels)")]
    [Tooltip("Disabled button cloned once per level; its label is replaced with 'Level N'.")]
    [SerializeField] private Button _levelButtonTemplate;

    // Anchor band the generated level buttons are laid out in, two per row,
    // matching the hand-placed Play/Quit rows above and below it.
    private const float BandTop = 0.40f;
    private const float RowHeight = 0.08f;
    private const float RowGap = 0.01f;

    [Header("Game Over Buttons")]
    [SerializeField] private Button _replayButton;
    [SerializeField] private Button _gameOverMenuButton;

    // Generated level buttons, kept so RefreshLevelButtonStates can update
    // lock state / best score in place without re-instantiating them.
    private readonly System.Collections.Generic.List<Button> _levelButtons = new();

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
        if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
        if (_leaderboardButton != null) _leaderboardButton.onClick.AddListener(OpenLeaderboard);
        BuildLevelButtons();
        if (_replayButton != null) _replayButton.onClick.AddListener(ReplayLevel);
        if (_gameOverMenuButton != null) _gameOverMenuButton.onClick.AddListener(ReturnToMenu);

        if (_levelManager != null)
            _levelManager.OnAllLevelsComplete += HandleAllLevelsComplete;

        _menuPanel.SetActive(!s_menuDismissed);
    }

    void OnDestroy()
    {
        if (_levelManager != null)
            _levelManager.OnAllLevelsComplete -= HandleAllLevelsComplete;
    }

    // One button per LevelData in LevelManager.levels, cloned from the
    // template: two columns per row, a lone last button centered.
    void BuildLevelButtons()
    {
        if (_levelButtonTemplate == null)
        {
            Debug.LogError("MenuController: level button template is not assigned.");
            return;
        }

        int count = (_levelManager != null && _levelManager.levels != null)
            ? _levelManager.levels.Length : 0;
        _levelButtonTemplate.gameObject.SetActive(false);
        _levelButtons.Clear();

        Transform parent = _levelButtonTemplate.transform.parent;

        for (int i = 0; i < count; i++)
        {
            int levelIndex = i;

            // Reuse a level button baked into the scene ("LevelNButton") if present,
            // so the menu is WYSIWYG in the Editor before Play; only clone the
            // template when a baked button is missing (keeps the old behaviour as a
            // fallback and auto-covers newly added levels).
            Button button = FindBakedLevelButton(parent, i);
            if (button == null)
            {
                button = Instantiate(_levelButtonTemplate, parent);
                button.name = "Level" + (i + 1) + "Button";
                PositionLevelButton(button, i, count);
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => StartLevel(levelIndex));
            button.gameObject.SetActive(true);
            _levelButtons.Add(button);
        }

        RefreshLevelButtonStates();
    }

    // Finds a level button pre-placed in the scene under the template's parent.
    // Skips the template itself, which historically shares the "Level1Button" name.
    Button FindBakedLevelButton(Transform parent, int index)
    {
        if (parent == null) return null;
        string wanted = "Level" + (index + 1) + "Button";
        foreach (Transform child in parent)
        {
            if (child.name != wanted) continue;
            if (_levelButtonTemplate != null && child == _levelButtonTemplate.transform) continue;
            return child.GetComponent<Button>();
        }
        return null;
    }

    // Updates label text, lock state and best-score display on the already
    // generated level buttons — called after build and whenever progress
    // may have changed (a level was just completed).
    void RefreshLevelButtonStates()
    {
        for (int i = 0; i < _levelButtons.Count; i++)
        {
            Button button = _levelButtons[i];
            bool unlocked = _progressManager == null || _progressManager.IsUnlocked(i);
            button.interactable = unlocked;

            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null)
                label.text = unlocked ? "Level " + (i + 1) : "Level " + (i + 1) + " — Locked";

            var bestScoreLabel = button.transform.Find("BestScoreLabel")?.GetComponent<TMPro.TMP_Text>();
            if (bestScoreLabel != null)
            {
                bool completed = _progressManager != null && _progressManager.IsCompleted(i);
                bestScoreLabel.text = completed ? "Best: " + _progressManager.GetBestScore(i) : "";
            }
        }
    }

    void PositionLevelButton(Button button, int index, int count)
    {
        int row = index / 2;
        bool loneLast = (index == count - 1) && (count % 2 == 1);

        float yMax = BandTop - row * (RowHeight + RowGap);
        float xMin, xMax;
        if (loneLast) { xMin = 0.42f; xMax = 0.58f; }
        else if (index % 2 == 0) { xMin = 0.33f; xMax = 0.49f; }
        else { xMin = 0.51f; xMax = 0.67f; }

        var rt = (RectTransform)button.transform;
        rt.anchorMin = new Vector2(xMin, yMax - RowHeight);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void StartLevel(int levelIndex)
    {
        if (_fader != null)
            _fader.RunTransition(() => BeginLevel(levelIndex));
        else
            BeginLevel(levelIndex);
    }

    // Midpoint of the fade: swap menu for a freshly built board, in place.
    void BeginLevel(int levelIndex)
    {
        s_menuDismissed = true;
        _menuPanel.SetActive(false);

        if (_levelManager != null)
            _levelManager.LoadLevelByIndex(levelIndex);
        else
            _levelSelect.LoadLevel(levelIndex); // fallback: legacy scene reload
    }

    void HandleAllLevelsComplete()
    {
        s_menuDismissed = false;
        _menuPanel.SetActive(true);
        RefreshLevelButtonStates();
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
        RefreshLevelButtonStates();
    }

    public void QuitGame()
    {
        _levelSelect.QuitGame();
    }

    public void OpenLeaderboard()
    {
        if (_leaderboard != null)
            _leaderboard.Show();
    }

    // For flows that reload the scene and must land on the menu
    // (e.g. finishing the last level) rather than auto-starting a level.
    public static void ShowMenuOnNextLoad()
    {
        s_menuDismissed = false;
    }
}
