using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local (on-device) high-score list: one row per level showing its best
/// score, driven by ProgressManager. No backend — everything is read from
/// the same save data LevelManager writes to on level completion.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("Panel (assign in Inspector)")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _closeButton;

    [Header("Rows (assign in Inspector)")]
    [Tooltip("Disabled TMP_Text cloned once per level; its text is replaced with the row content.")]
    [SerializeField] private RectTransform _rowTemplate;

    [Header("References (assign in Inspector)")]
    [SerializeField] private LevelManager _levelManager;
    [SerializeField] private ProgressManager _progressManager;

    // Anchor band the generated rows are stacked in, one per row.
    private const float BandTop = 0.80f;
    private const float RowHeight = 0.07f;
    private const float RowGap = 0.01f;

    private readonly List<RectTransform> _rows = new();

    void Awake()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
        if (_panel != null) _panel.SetActive(false);
    }

    public void Show()
    {
        if (_panel == null || _rowTemplate == null || _levelManager == null)
        {
            Debug.LogError("LeaderboardUI: panel, row template or LevelManager reference is missing.");
            return;
        }

        BuildRows();
        _panel.SetActive(true);
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    void BuildRows()
    {
        foreach (var row in _rows)
            if (row != null) Destroy(row.gameObject);
        _rows.Clear();

        int count = _levelManager.levels != null ? _levelManager.levels.Length : 0;
        for (int i = 0; i < count; i++)
        {
            RectTransform row = Instantiate(_rowTemplate, _rowTemplate.parent);
            row.name = "LeaderboardRow" + (i + 1);
            row.gameObject.SetActive(true);
            PositionRow(row, i);

            var label = row.GetComponent<TMPro.TMP_Text>();
            if (label != null) label.text = BuildRowText(i);

            _rows.Add(row);
        }
    }

    string BuildRowText(int levelIndex)
    {
        string levelName = "Level " + (levelIndex + 1);

        bool unlocked = _progressManager == null || _progressManager.IsUnlocked(levelIndex);
        if (!unlocked)
            return levelName + " — Locked";

        bool completed = _progressManager != null && _progressManager.IsCompleted(levelIndex);
        return completed
            ? levelName + " — Best: " + _progressManager.GetBestScore(levelIndex)
            : levelName + " — Not completed";
    }

    void PositionRow(RectTransform row, int index)
    {
        float yMax = BandTop - index * (RowHeight + RowGap);
        row.anchorMin = new Vector2(0.2f, yMax - RowHeight);
        row.anchorMax = new Vector2(0.8f, yMax);
        row.offsetMin = Vector2.zero;
        row.offsetMax = Vector2.zero;
    }
}
