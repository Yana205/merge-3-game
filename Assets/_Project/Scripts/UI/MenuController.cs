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

    [Header("Menu Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _quitButton;

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

        for (int i = 0; i < count; i++)
        {
            int levelIndex = i;
            Button button = Instantiate(_levelButtonTemplate, _levelButtonTemplate.transform.parent);
            button.name = "Level" + (i + 1) + "Button";
            button.onClick.AddListener(() => StartLevel(levelIndex));

            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null) label.text = "Level " + (i + 1);

            PositionLevelButton(button, i, count);
            button.gameObject.SetActive(true);
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

    // For flows that reload the scene and must land on the menu
    // (e.g. finishing the last level) rather than auto-starting a level.
    public static void ShowMenuOnNextLoad()
    {
        s_menuDismissed = false;
    }
}
