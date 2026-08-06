using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The supply the player draws from: the crystal about to be placed, plus the two
/// behind it.
///
/// This is the answer to the complaint that killed both previous designs — that
/// crystals appeared on their own, somewhere the player did not choose, with no
/// warning. Nothing is generated at placement time; the queue is rolled ahead and
/// shown, so every crystal is visible three moves before it has to be dealt with.
/// A red in slot 3 is a problem the player has two turns to plan around.
///
/// Previews are rendered as REAL world-space Items under the board rather than as UI
/// Toolkit images. That reuses the existing sprite, GemConfig and pooling machinery
/// exactly as the board does — a preview cannot drift out of sync with what actually
/// lands, because it is built by the same call. It also puts them where the player is
/// already looking, which the old side-panel HUD row demonstrably was not.
/// </summary>
public class CrystalQueue : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    [SerializeField] private GridManager gridManager;

    [Header("Queue")]
    [Tooltip("Crystals visible at once: the one being placed plus its lookahead. " +
             "Three is enough to plan a cluster around a red without turning the " +
             "board into a spreadsheet.")]
    [Min(1)]
    [SerializeField] private int visibleCount = 3;

    [Header("Preview layout")]
    [Tooltip("Gap below the bottom row of the board, in cells.")]
    [SerializeField] private float dropBelowBoard = 1.4f;

    [Tooltip("Spacing between preview crystals, in cells.")]
    [SerializeField] private float previewSpacing = 1.1f;

    // The rolled-but-unplaced crystals. Plain data: nothing here can be left
    // dangling by the pool, because none of it is a live Item.
    private readonly List<CrystalSpec> _queue = new List<CrystalSpec>();

    // The throwaway Items drawn under the board. Rebuilt wholesale whenever the
    // queue moves — three items a turn is nothing, and it removes any chance of a
    // preview disagreeing with the queue behind it.
    private readonly List<Item> _previews = new List<Item>();

    private DifficultyCurve _difficulty;
    private int _score;

    /// <summary>The crystal the next placement will use.</summary>
    public CrystalSpec Next => _queue.Count > 0 ? _queue[0] : new CrystalSpec(GemFamily.Standard, 1);

    public bool HasNext => _queue.Count > 0;

    void OnEnable()  => GameEvents.ScoreChanged += HandleScoreChanged;
    void OnDisable() => GameEvents.ScoreChanged -= HandleScoreChanged;

    /// <summary>
    /// Fill a fresh queue for a new run.
    ///
    /// Must be called AFTER the board is built. CreateGrid runs ClearGrid, which
    /// reclaims every Item this manager spawned — including the previews — so
    /// rendering them first would leave three dead references and an empty row.
    /// </summary>
    public void ResetRun(DifficultyCurve difficulty, int score)
    {
        _difficulty = difficulty;
        _score = score;

        ClearPreviews();
        _queue.Clear();
        Refill();
        Render();
        Announce();
    }

    /// <summary>
    /// Pop the front crystal and slide the queue up. Returns the crystal that was
    /// taken; the caller is responsible for actually putting it on the board.
    /// </summary>
    public CrystalSpec Take()
    {
        CrystalSpec taken = Next;

        if (_queue.Count > 0)
            _queue.RemoveAt(0);

        Refill();
        Render();
        Announce();
        return taken;
    }

    private void HandleScoreChanged(int total)
    {
        // Tracked rather than asked for, so this class needs no reference to
        // ScoreController. Only affects crystals rolled from here on — entries
        // already visible in the preview never change under the player, which is
        // the entire point of showing them.
        _score = total;
    }

    private void Refill()
    {
        if (_difficulty == null) return;

        while (_queue.Count < visibleCount)
            _queue.Add(_difficulty.Roll(_score));
    }

    // --- Rendering ----------------------------------------------------------

    private void Render()
    {
        ClearPreviews();
        if (gridManager == null) return;

        float cell = gridManager.cellSize;
        float y = gridManager.BottomEdgeY - dropBelowBoard * cell;

        // Centre the row under the board, whatever the count.
        float span = (_queue.Count - 1) * previewSpacing * cell;
        float startX = gridManager.BoardCentre.x - span / 2f;

        for (int i = 0; i < _queue.Count; i++)
        {
            var pos = new Vector3(startX + i * previewSpacing * cell, y, 0f);
            Item preview = gridManager.SpawnLooseItem(pos, _queue[i].Tier, _queue[i].Family);
            if (preview != null)
                _previews.Add(preview);
        }
    }

    private void ClearPreviews()
    {
        if (gridManager != null)
            foreach (Item preview in _previews)
                if (preview != null) gridManager.DespawnItem(preview);

        _previews.Clear();
    }

    private void Announce()
    {
        GameEvents.RaiseQueueChanged(_queue.Count);
    }
}
