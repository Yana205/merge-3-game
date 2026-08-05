using System.Collections;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements (assign in Inspector)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI targetText;
    public GameObject gameOverPanel;

    // Kept wired but currently uncalled: the endless board has no stages left to
    // announce. The CanvasGroup is already hooked up in the scene, so retaining
    // the seam costs nothing and saves re-wiring it by hand for the next thing
    // worth announcing (a new personal best, a big combo).
    [Header("Banner (assign in Inspector)")]
    public CanvasGroup levelBanner;
    public TextMeshProUGUI levelBannerText;
    [SerializeField] private float bannerHold = 0.9f;
    [SerializeField] private float bannerFade = 0.25f;

    [Header("Events (assign in Inspector)")]
    [SerializeField] private LevelManager levelManager;

    private Coroutine _bannerRoutine;
    private int _score;
    private int _best;

    // Subscribe in Awake so we never miss the initial OnScoreChanged
    // fired from LevelManager.Start when services are already ready.
    void Awake()
    {
        if (levelManager != null)
        {
            levelManager.OnScoreChanged += UpdateScore;
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
        }
    }

    void Start()
    {
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

    // Brief fading banner across the middle of the screen. See the field comment
    // above — no caller right now.
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
    public void UpdateScore(int currentScore, int bestScore)
    {
        _score = currentScore;
        _best = bestScore;
        RefreshHud();
    }

    void RefreshHud()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + _score;

        if (targetText != null)
            targetText.text = "Best  " + _best;
    }

    public void ShowGameOver(int score, int bestScore)
    {
        if (gameOverPanel == null) return;

        var text = gameOverPanel.transform.Find("GameOverText")?.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = "GAME OVER\n\nSCORE  " + score + "\nBEST  " + bestScore;

        gameOverPanel.SetActive(true);
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}
