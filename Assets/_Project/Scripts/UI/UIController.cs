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
    private Button _musicButton;
    private Button _sfxButton;
    private Label _placeHint;
    private Label _stageLabel;
    private VisualElement _stageBarFill;
    private Label _toast;
    private IVisualElementScheduledItem _toastHide;

    // Stage window for the progress bar: the score it began at and the score the
    // next stage begins at.
    private int _stageStart;
    private int _stageNext = 1;

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
        _musicButton = root.Q<Button>("music-button");
        _sfxButton = root.Q<Button>("sfx-button");
        _placeHint = root.Q<Label>("place-hint");
        _stageLabel = root.Q<Label>("stage-label");
        _stageBarFill = root.Q<VisualElement>("stage-bar-fill");
        _toast = root.Q<Label>("toast");

        if (_scoreLabel == null || _highScoreLabel == null || _restartButton == null)
            Debug.LogError("UIController: one or more HUD elements not found — check the name= attributes in GameHUD.uxml.");

        _shownScore = 0;
        _targetScore = 0;
        if (_scoreLabel != null) _scoreLabel.text = "0";
        SetHighScore(0);   // best arrives over the bus from ProgressManager
        RefreshAudioLabels();

        // ~30fps ticker that walks the shown score toward the target. Started
        // paused; ScoreChanged resumes it when there is distance to cover.
        if (_scoreLabel != null)
        {
            _countTicker = _scoreLabel.schedule.Execute(TickCountUp).Every(33);
            _countTicker.Pause();
        }

        if (_restartButton != null)
            _restartButton.clicked += OnRestartClicked;
        if (_musicButton != null)
            _musicButton.clicked += AudioDirector.ToggleMusic;
        if (_sfxButton != null)
            _sfxButton.clicked += AudioDirector.ToggleSfx;

        GameEvents.ScoreChanged += OnScoreChanged;
        GameEvents.BestScoreChanged += SetHighScore;
        GameEvents.StageChanged += OnStageChanged;
        GameEvents.ColorUnlocked += OnColorUnlocked;
        AudioDirector.MuteChanged += RefreshAudioLabels;
    }

    void OnDisable()
    {
        GameEvents.ScoreChanged -= OnScoreChanged;
        GameEvents.BestScoreChanged -= SetHighScore;
        GameEvents.StageChanged -= OnStageChanged;
        GameEvents.ColorUnlocked -= OnColorUnlocked;
        AudioDirector.MuteChanged -= RefreshAudioLabels;
        _toastHide?.Pause();
        _toastHide = null;

        _countTicker?.Pause();
        _countTicker = null;
        _punchRelease?.Pause();
        _punchRelease = null;

        if (_restartButton != null)
            _restartButton.clicked -= OnRestartClicked;
        if (_musicButton != null)
            _musicButton.clicked -= AudioDirector.ToggleMusic;
        if (_sfxButton != null)
            _sfxButton.clicked -= AudioDirector.ToggleSfx;
    }

    // --- Score --------------------------------------------------------------

    // Bus handler — the score changed somewhere; roll the label toward it and
    // punch. A reset to 0 (new run) snaps instantly: counting DOWN reads as losing
    // points the player didn't lose.
    private void OnScoreChanged(int total)
    {
        UpdateStageBar(total);
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

    // --- Stage ---------------------------------------------------------------

    private int _lastStage = -1;

    private void OnStageChanged(int stage, int colours, int stageStart, int nextStart)
    {
        _stageStart = stageStart;
        _stageNext = Mathf.Max(stageStart + 1, nextStart);
        if (_stageLabel != null) _stageLabel.text = (stage + 1).ToString();
        UpdateStageBar(_targetScore);

        bool up = _lastStage >= 0 && stage > _lastStage && stageStart > 0;
        _lastStage = stage;
        if (up)
        {
            _stageLabel?.AddToClassList("score-numeral--punch");
            _stageLabel?.schedule.Execute(() => _stageLabel.RemoveFromClassList("score-numeral--punch")).StartingIn(140);
            ShowToast("STAGE " + (stage + 1), new Color(1f, 0.85f, 0.35f));
        }
    }

    private void OnColorUnlocked(int colourIndex)
    {
        ShowToast("NEW GEM: " + GemPalette.NameFor(colourIndex), Color.Lerp(GemPalette.ColorFor(colourIndex), Color.white, 0.25f));
    }

    private void UpdateStageBar(int score)
    {
        if (_stageBarFill == null) return;
        float k = Mathf.Clamp01((score - _stageStart) / (float)Mathf.Max(1, _stageNext - _stageStart));
        _stageBarFill.style.width = Length.Percent(k * 100f);
    }

    // One line at the top of the screen for a few seconds — the same slot the
    // first-move hint uses, so messages never stack.
    private void ShowToast(string text, Color colour)
    {
        if (_toast == null) return;
        _toast.text = text;
        _toast.style.color = colour;
        _toast.RemoveFromClassList("toast--hidden");
        _toastHide?.Pause();
        _toastHide = _toast.schedule.Execute(() => _toast.AddToClassList("toast--hidden")).StartingIn(2400);
    }

    private void SetHighScore(int best)
    {
        if (_highScoreLabel != null)
            _highScoreLabel.text = best.ToString();
    }

    // --- Sound --------------------------------------------------------------

    // Both toggles read the shared static state, so the HUD and the title
    // menu's MUSIC entry always agree — flipping one refreshes the other via
    // AudioDirector.MuteChanged.
    private void RefreshAudioLabels()
    {
        if (_musicButton != null)
            _musicButton.text = AudioDirector.MusicMuted ? "MUSIC: OFF" : "MUSIC: ON";
        if (_sfxButton != null)
            _sfxButton.text = AudioDirector.SfxMuted ? "SFX: OFF" : "SFX: ON";
    }

    // Button click -> ask for a fresh run over the bus. Still self-contained: the
    // HUD holds no references into gameplay systems, it just says what happened.
    private void OnRestartClicked()
    {
        GameEvents.RaiseRestartRequested();
    }
}
