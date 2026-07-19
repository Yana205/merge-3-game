using System.Collections;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements (assign in Inspector)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI targetText;
    public GameObject levelCompletePanel;
    public GameObject gameOverPanel;

    [Header("Level Banner (assign in Inspector)")]
    public CanvasGroup levelBanner;
    public TextMeshProUGUI levelBannerText;
    [SerializeField] private float bannerHold = 0.9f;
    [SerializeField] private float bannerFade = 0.25f;

    [Header("Events (assign in Inspector)")]
    [SerializeField] private LevelManager levelManager;

    private Coroutine _bannerRoutine;
    private int _level = 1;
    private int _score;
    private int _target;

    // Subscribe in Awake so we never miss the initial OnScoreChanged
    // fired from LevelManager.Start when services are already ready.
    void Awake()
    {
        if (levelManager != null)
        {
            levelManager.OnScoreChanged += UpdateScore;
            levelManager.OnLevelComplete += ShowLevelComplete;
            levelManager.OnLevelChanged += UpdateLevel;
        }
        else
        {
            Debug.LogError("UIManager: levelManager is not assigned — score UI will not update.");
        }
    }

    void OnDestroy()
    {
        if (levelManager != null)
        {
            levelManager.OnScoreChanged -= UpdateScore;
            levelManager.OnLevelComplete -= ShowLevelComplete;
            levelManager.OnLevelChanged -= UpdateLevel;
        }
    }

    void Start()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (levelBanner != null)
            levelBanner.gameObject.SetActive(false);
    }

    // Hide the score/level HUD text while the main menu overlay is up (it would
    // otherwise sit on top of the title logo); shown again when a run begins.
    public void SetHudVisible(bool visible)
    {
        if (scoreText != null) scoreText.gameObject.SetActive(visible);
        if (targetText != null) targetText.gameObject.SetActive(visible);
    }

    // Brief "Level N" banner shown when a level starts.
    public void ShowLevelBanner(string text)
    {
        if (levelBanner == null || levelBannerText == null)
            return;

        levelBannerText.text = text;
        if (_bannerRoutine != null)
            StopCoroutine(_bannerRoutine);
        _bannerRoutine = StartCoroutine(BannerRoutine());
    }

    IEnumerator BannerRoutine()
    {
        levelBanner.gameObject.SetActive(true);
        yield return FadeBanner(0f, 1f);
        yield return new WaitForSecondsRealtime(bannerHold);
        yield return FadeBanner(1f, 0f);
        levelBanner.gameObject.SetActive(false);
        _bannerRoutine = null;
    }

    IEnumerator FadeBanner(float from, float to)
    {
        float t = 0f;
        while (t < bannerFade)
        {
            t += Time.unscaledDeltaTime;
            levelBanner.alpha = Mathf.Lerp(from, to, t / bannerFade);
            yield return null;
        }
        levelBanner.alpha = to;
    }

    // FUTURE: add animated score counter
    public void UpdateScore(int currentScore, int targetScore)
    {
        _score = currentScore;
        _target = targetScore;
        RefreshHud();
    }

    public void UpdateLevel(int level)
    {
        _level = level;
        RefreshHud();
    }

    void RefreshHud()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + _score;

        if (targetText != null)
            targetText.text = "Level " + _level + "   ·   Next: " + _target;
    }

    // FUTURE: add star rating, particle burst
    public void ShowLevelComplete()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(true);
    }

    public void HideLevelComplete()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
    }

    public void ShowGameOver(int score, int levelReached)
    {
        if (gameOverPanel == null) return;

        var text = gameOverPanel.transform.Find("GameOverText")?.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = "Game Over\n\nScore  " + score + "\nReached  Level " + levelReached;

        gameOverPanel.SetActive(true);
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}
