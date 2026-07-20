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

    // Anchor band the generated rows are stacked in, one per row (fits 10).
    private const float BandTop = 0.80f;
    private const float RowHeight = 0.062f;
    private const float RowGap = 0.008f;

    private readonly List<RectTransform> _rows = new();

    void Awake()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
        if (_panel != null) _panel.SetActive(false);
    }

    public void Show()
    {
        if (_panel == null || _rowTemplate == null)
        {
            Debug.LogError("LeaderboardUI: panel or row template reference is missing.");
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

        var runs = _progressManager != null
            ? _progressManager.GetTopRuns()
            : new System.Collections.Generic.List<RunEntry>();

        if (runs.Count == 0)
        {
            RectTransform empty = Instantiate(_rowTemplate, _rowTemplate.parent);
            empty.name = "LeaderboardRowEmpty";
            empty.gameObject.SetActive(true);
            PositionRow(empty, 0);
            var lbl = empty.GetComponent<TMPro.TMP_Text>();
            if (lbl != null) lbl.text = "No runs yet — press Play!";
            _rows.Add(empty);
            return;
        }

        for (int i = 0; i < runs.Count; i++)
        {
            RectTransform row = Instantiate(_rowTemplate, _rowTemplate.parent);
            row.name = "LeaderboardRow" + (i + 1);
            row.gameObject.SetActive(true);
            PositionRow(row, i);

            var label = row.GetComponent<TMPro.TMP_Text>();
            if (label != null) label.text = BuildRowText(i, runs[i]);

            _rows.Add(row);
        }
    }

    // "1.   Score 1240      Level 7"
    string BuildRowText(int rank, RunEntry run)
    {
        return string.Format("{0,2}.   Score {1,-6}   Level {2}", rank + 1, run.score, run.level);
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
