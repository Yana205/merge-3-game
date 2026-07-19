using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the UI Toolkit HUD (GameHUD.uxml). Queries its elements by name with
/// Q&lt;T&gt;(), then keeps them in sync at runtime by listening to the global
/// <see cref="GameEvents.ScoreChanged"/> bus — this is the "UIController listens to
/// ScoreChanged" half of the Observer story started in Lesson 1, so the HUD never
/// references ScoreController or LevelManager directly.
///
/// Lives on the same GameObject as the <see cref="UIDocument"/>.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class UIController : MonoBehaviour
{
    [Tooltip("PlayerPrefs key the best score is persisted under.")]
    [SerializeField] private string highScoreKey = "Merge3_HighScore";

    private UIDocument _document;
    private Label _scoreLabel;
    private Label _highScoreLabel;
    private Button _restartButton;

    private int _highScore;

    // Query + subscribe in OnEnable; UIDocument builds rootVisualElement in its own
    // OnEnable, so keep this component on the same GameObject (its UIDocument runs
    // first). Every subscription here has a matching removal in OnDisable.
    void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        VisualElement root = _document != null ? _document.rootVisualElement : null;
        if (root == null)
        {
            Debug.LogError("UIController: UIDocument has no rootVisualElement — is a source asset (GameHUD.uxml) assigned?");
            return;
        }

        // Q<T>("name") — names must match the UXML exactly or these return null.
        _scoreLabel = root.Q<Label>("score-label");
        _highScoreLabel = root.Q<Label>("high-score-label");
        _restartButton = root.Q<Button>("restart-button");

        if (_scoreLabel == null || _highScoreLabel == null || _restartButton == null)
            Debug.LogError("UIController: one or more HUD elements not found — check the name= attributes in GameHUD.uxml.");

        _highScore = PlayerPrefs.GetInt(highScoreKey, 0);
        SetScore(0);
        SetHighScore(_highScore);

        if (_restartButton != null)
            _restartButton.clicked += OnRestartClicked;

        GameEvents.ScoreChanged += OnScoreChanged;
    }

    void OnDisable()
    {
        GameEvents.ScoreChanged -= OnScoreChanged;

        if (_restartButton != null)
            _restartButton.clicked -= OnRestartClicked;
    }

    // Bus handler — the score changed somewhere; reflect it and track the best.
    private void OnScoreChanged(int total)
    {
        SetScore(total);

        if (total > _highScore)
        {
            _highScore = total;
            PlayerPrefs.SetInt(highScoreKey, _highScore);
            SetHighScore(_highScore);
        }
    }

    private void SetScore(int total)
    {
        if (_scoreLabel != null)
            _scoreLabel.text = "Score: " + total;
    }

    private void SetHighScore(int best)
    {
        if (_highScoreLabel != null)
            _highScoreLabel.text = "Best: " + best;
    }

    // Button click -> restart the current scene. Self-contained so the HUD needs
    // no references into gameplay systems.
    private void OnRestartClicked()
    {
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }
}
