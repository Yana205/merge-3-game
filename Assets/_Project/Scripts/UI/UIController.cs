using UnityEngine;
using UnityEngine.UIElements;

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
    [Tooltip("Optional. Assign to let the HUD's SHATTER button arm the pickaxe. " +
             "Without it the counter still tracks charges over the bus; only the " +
             "button goes inert.")]
    [SerializeField] private PickaxeController pickaxe;

    private UIDocument _document;
    private Label _scoreLabel;
    private Label _highScoreLabel;
    private Button _restartButton;
    private Label _pickaxeLabel;
    private Button _pickaxeButton;
    private Label _rescueHint;
    private Button _muteButton;

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

        SetScore(0);
        // Best arrives over the bus. It used to be tracked here in its own
        // PlayerPrefs key, separate from the leaderboard's store — two records of
        // the same number that could disagree. ProgressManager owns it now.
        SetHighScore(0);

        _pickaxeLabel = root.Q<Label>("pickaxe-label");
        _pickaxeButton = root.Q<Button>("pickaxe-button");
        _rescueHint = root.Q<Label>("rescue-hint");
        _muteButton = root.Q<Button>("mute-button");

        SetPickaxe(0, false);
        SetRescuePending(false);
        SetMuteLabel(AudioDirector.Muted);

        if (_restartButton != null)
            _restartButton.clicked += OnRestartClicked;
        if (_pickaxeButton != null)
            _pickaxeButton.clicked += OnPickaxeClicked;
        if (_muteButton != null)
            _muteButton.clicked += OnMuteClicked;

        GameEvents.ScoreChanged += OnScoreChanged;
        GameEvents.BestScoreChanged += SetHighScore;
        GameEvents.PickaxeChanged += SetPickaxe;
        GameEvents.JamRescuePending += SetRescuePending;
    }

    void OnDisable()
    {
        GameEvents.ScoreChanged -= OnScoreChanged;
        GameEvents.BestScoreChanged -= SetHighScore;
        GameEvents.PickaxeChanged -= SetPickaxe;
        GameEvents.JamRescuePending -= SetRescuePending;

        if (_restartButton != null)
            _restartButton.clicked -= OnRestartClicked;
        if (_pickaxeButton != null)
            _pickaxeButton.clicked -= OnPickaxeClicked;
        if (_muteButton != null)
            _muteButton.clicked -= OnMuteClicked;
    }

    // --- Pickaxe ------------------------------------------------------------

    // Bus handler. The counter and the button's three states (empty / ready /
    // armed) are driven entirely from here, so the HUD never has to ask the
    // PickaxeController what it is doing — it is told.
    private void SetPickaxe(int charges, bool armed)
    {
        if (_pickaxeLabel != null)
            _pickaxeLabel.text = charges.ToString();

        if (_pickaxeButton == null) return;

        _pickaxeButton.EnableInClassList("pickaxe-button--empty", charges <= 0);
        _pickaxeButton.EnableInClassList("pickaxe-button--armed", armed);
        _pickaxeButton.text = armed ? "TAP A GEM" : "SHATTER";
    }

    private void SetRescuePending(bool pending)
    {
        if (_rescueHint != null)
            _rescueHint.EnableInClassList("rescue-hint--visible", pending);
    }

    private void OnPickaxeClicked()
    {
        if (pickaxe != null) pickaxe.ToggleArmed();
    }

    // --- Sound --------------------------------------------------------------

    private void OnMuteClicked()
    {
        AudioDirector.SetMuted(!AudioDirector.Muted);
        SetMuteLabel(AudioDirector.Muted);
    }

    private void SetMuteLabel(bool muted)
    {
        if (_muteButton != null)
            _muteButton.text = muted ? "SOUND: OFF" : "SOUND: ON";
    }

    // Bus handler — the score changed somewhere; reflect it. Working out whether
    // that beat the record is ProgressManager's job, and it says so via
    // BestScoreChanged.
    private void OnScoreChanged(int total)
    {
        SetScore(total);
    }

    // The UXML carries the SCORE / BEST captions in their own elements,
    // so these labels hold the bare numeral.
    private void SetScore(int total)
    {
        if (_scoreLabel != null)
            _scoreLabel.text = total.ToString();
    }

    private void SetHighScore(int best)
    {
        if (_highScoreLabel != null)
            _highScoreLabel.text = best.ToString();
    }

    // Button click -> ask for a fresh run over the bus. Still self-contained: the
    // HUD holds no references into gameplay systems, it just says what happened.
    //
    // This used to reload the scene, which produced a dead screen: MenuController's
    // s_menuDismissed is static and only resets at app start, so the reloaded scene
    // showed no menu, and LevelManager.autoStartOnLoad is false so no run began —
    // leaving no menu and a null grid with nothing to click.
    private void OnRestartClicked()
    {
        GameEvents.RaiseRestartRequested();
    }
}
