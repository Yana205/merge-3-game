using UnityEngine;

/// <summary>
/// The run's escape hatch. The player banks a pickaxe charge every
/// <see cref="scorePerCharge"/> points and spends it to shatter any one gem,
/// freeing its cell.
///
/// It exists because the red chain and the top of the standard ladder both produce
/// gems that can never merge again. Without a way to remove one, a run can reach a
/// state the player cannot play out of through any sequence of legal moves — the
/// board is simply lost, several moves before the game admits it.
///
/// A pure Observer of the <see cref="GameEvents"/> bus, like ScoreController: it
/// watches the score and announces its own state. It holds no reference to the
/// grid, the HUD, or the input layer, and none of them reference it — InputHandler
/// is handed one in the inspector because it must ask "am I armed?" every click,
/// which is a question, not a dependency on gameplay rules.
/// </summary>
public class PickaxeController : MonoBehaviour
{
    [Header("Earning")]
    [Tooltip("Points between charges. Every time the score crosses another multiple " +
             "of this, one charge is banked.")]
    [Min(1)]
    [SerializeField] private int scorePerCharge = 150;

    [Tooltip("Most charges the player may hold at once. Uncapped, a long run banks " +
             "a dozen and the escape hatch stops being a rescue — it becomes a " +
             "stockpile that removes the pressure entirely.")]
    [Min(1)]
    [SerializeField] private int maxCharges = 3;

    [Header("References (assign in Inspector)")]
    [SerializeField] private GridManager gridManager;

    /// <summary>Charges in hand.</summary>
    public int Charges { get; private set; }

    /// <summary>True while the next tap on a gem will shatter it.</summary>
    public bool IsArmed { get; private set; }

    public bool HasCharge => Charges > 0;

    // Score at which the next charge is earned. Tracked as a moving goalpost rather
    // than recomputed as score/scorePerCharge so that ResetRun can rewind it
    // without the old run's total leaking a free charge into the new one.
    private int _nextChargeAt;

    void OnEnable()
    {
        GameEvents.ScoreChanged += HandleScoreChanged;
    }

    void OnDisable()
    {
        GameEvents.ScoreChanged -= HandleScoreChanged;
    }

    /// <summary>
    /// Wipe charges for a fresh run. Called by LevelManager alongside the score
    /// reset — banked charges surviving into a new run would hand the player a free
    /// rescue they did not earn.
    /// </summary>
    public void ResetRun()
    {
        Charges = 0;
        IsArmed = false;
        _nextChargeAt = scorePerCharge;
        Announce();
    }

    // The score moved. Award every threshold it crossed, not just one: a single
    // high-tier merge can jump more than scorePerCharge in one go, and awarding one
    // charge per event would quietly swallow the rest.
    private void HandleScoreChanged(int total)
    {
        // A reset to 0 rewinds the goalpost; without this, restarting mid-run would
        // leave _nextChargeAt far ahead and the next run would earn nothing.
        if (total < _nextChargeAt - scorePerCharge)
            _nextChargeAt = scorePerCharge;

        bool gained = false;
        while (total >= _nextChargeAt)
        {
            _nextChargeAt += scorePerCharge;
            if (Charges < maxCharges) { Charges++; gained = true; }
        }

        if (gained) Announce();
    }

    /// <summary>
    /// Arm the tool if there is a charge to spend. Returns false when empty, so the
    /// caller can play a rejection instead of silently doing nothing.
    /// </summary>
    public bool TryArm()
    {
        if (!HasCharge || IsArmed) return false;
        IsArmed = true;
        Announce();
        return true;
    }

    public void Disarm()
    {
        if (!IsArmed) return;
        IsArmed = false;
        Announce();
    }

    public void ToggleArmed()
    {
        if (IsArmed) Disarm();
        else TryArm();
    }

    /// <summary>
    /// Spend a charge to remove <paramref name="item"/> from the board. Returns
    /// false if there was nothing to spend, no item, or the item is not on the grid.
    /// </summary>
    public bool Shatter(Item item)
    {
        if (!HasCharge || item == null || gridManager == null) return false;

        Cell cell = gridManager.FindCellWithItem(item);
        if (cell == null) return false;

        // Announce before the teardown: listeners want the gem's tier, family and
        // position to place a burst and pick a sound, and DespawnItem recycles the
        // Item through the pool, which resets all of it.
        GameEvents.RaiseTileShattered(item, cell);

        cell.RemoveItem();
        gridManager.DespawnItem(item);

        Charges--;
        IsArmed = false;
        Announce();
        return true;
    }

    private void Announce()
    {
        GameEvents.RaisePickaxeChanged(Charges, IsArmed);
    }
}
