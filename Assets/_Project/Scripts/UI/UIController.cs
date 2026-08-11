using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drives the UI Toolkit HUD (GameHUD.uxml). Queries its elements by name with
/// Q&lt;T&gt;(), then keeps them in sync at runtime by listening to the global
/// <see cref="GameEvents"/> bus — the HUD never references ScoreController or
/// LevelManager directly.
///
/// Feel: the score never just changes. It counts up toward the real total (a
/// scheduled tick) and punches — a USS class snaps it large and bright, and the
/// stylesheet's transition eases it back — so every match is visibly banked.
///
/// Lives on the same GameObject as the <see cref="UIDocument"/>.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class UIController : MonoBehaviour
{
    [Tooltip("Seconds the displayed score takes to catch up with the real total.")]
    [SerializeField] private float countUpTime = 0.35f;

    private UIDocument _document;
    private Label _scoreLabel;
    private Label _highScoreLabel;
    private Button _restartButton;
    private Button _muteButton;
    private Label _placeHint;

    // Count-up state: what the label shows vs what the score really is. The
    // scheduled ticker is held so OnDisable can stop it — a scheduler left
    // running against a torn-down element is a leak.
    private int _shownScore;
    private int _targetScore;
    private float _countSpeed;
    private IVisualElementScheduledItem _countTicker;
    private IVisualElementScheduledItem _punchRelease;

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
        _muteButton = root.Q<Button>("mute-button");
        _placeHint = root.Q<Label>("place-hint");

        if (_scoreLabel == null || _highScoreLabel == null || _restartButton == null)
            Debug.LogError("UIController: one or more HUD elements not found — check the name= attributes in GameHUD.uxml.");

        _shownScore = 0;
        _targetScore = 0;
        if (_scoreLabel != null) _scoreLabel.text = "0";
        SetHighScore(0);   // best arrives over the bus from ProgressManager
        SetMuteLabel(AudioDirector.Muted);

        // ~30fps ticker that walks the shown score toward the target. Started
        // paused; ScoreChanged resumes it when there is distance to cover.
        if (_scoreLabel != null)
        {
            _countTicker = _scoreLabel.schedule.Execute(TickCountUp).Every(33);
            _countTicker.Pause();
        }

        if (_restartButton != null)
            _restartButton.clicked += OnRestartClicked;
        if (_muteButton != null)
            _muteButton.clicked += OnMuteClicked;

        GameEvents.ScoreChanged += OnScoreChanged;
        GameEvents.BestScoreChanged += SetHighScore;
    }

    void OnDisable()
    {
        GameEvents.ScoreChanged -= OnScoreChanged;
        GameEvents.BestScoreChanged -= SetHighScore;

        _countTicker?.Pause();
        _countTicker = null;
        _punchRelease?.Pause();
        _punchRelease = null;

        if (_restartButton != null)
            _restartButton.clicked -= OnRestartClicked;
        if (_muteButton != null)
            _muteButton.clicked -= OnMuteClicked;
    }

    // --- Score --------------------------------------------------------------

    // Bus handler — the score changed somewhere; roll the label toward it and
    // punch. A reset to 0 (new run) snaps instantly: counting DOWN reads as losing
    // points the player didn't lose.
    private void OnScoreChanged(int total)
    {
        if (_scoreLabel == null) return;

        if (total < _targetScore)
        {
            _targetScore = total;
            _shownScore = total;
            _scoreLabel.text = total.ToString();
            _countTicker?.Pause();
            return;
        }

        if (total == _targetScore) return;

        _targetScore = total;
        _countSpeed = Mathf.Max(30f, (_targetScore - _shownScore) / Mathf.Max(0.05f, countUpTime));
        _countTicker?.Resume();
        Punch();

        // Retire the hint once the player has scored — they clearly found the verb.
        if (total > 0)
            _placeHint?.AddToClassList("place-hint--done");
    }

    private void TickCountUp()
    {
        if (_scoreLabel == null) return;

        _shownScore = Mathf.Min(_targetScore, _shownScore + Mathf.CeilToInt(_countSpeed * 0.033f));
        _scoreLabel.text = _shownScore.ToString();

        if (_shownScore >= _targetScore)
            _countTicker?.Pause();
    }

    // Snap big via the class, ease back via the USS transition on its removal.
    private void Punch()
    {
        if (_scoreLabel == null) return;
        _scoreLabel.AddToClassList("score-numeral--punch");
        _punchRelease?.Pause();
        _punchRelease = _scoreLabel.schedule
            .Execute(() => _scoreLabel.RemoveFromClassList("score-numeral--punch"))
            .StartingIn(90);
    }

    private void SetHighScore(int best)
    {
        if (_highScoreLabel != null)
            _highScoreLabel.text = best.ToString();
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

    // Button click -> ask for a fresh run over the bus. Still self-contained: the
    // HUD holds no references into gameplay systems, it just says what happened.
    private void OnRestartClicked()
    {
        GameEvents.RaiseRestartRequested();
    }
}
