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

    private Coroutine _bannerRoutine;

    void Start()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (levelBanner != null)
            levelBanner.gameObject.SetActive(false);
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
        if (scoreText != null)
            scoreText.text = "Score: " + currentScore;

        if (targetText != null)
            targetText.text = "Target: " + targetScore;
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

    public void ShowGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}
