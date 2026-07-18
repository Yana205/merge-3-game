using UnityEngine;

/// <summary>
/// Owns the running score. It is a pure Observer of the global <see cref="GameEvents"/>
/// bus: it adds points when a tile merges and writes to disk when a save is
/// requested. No other system calls into it to change the score anymore — they
/// raise bus events and this reacts, so ScoreController and the gameplay code that
/// causes scoring never reference each other directly.
/// </summary>
public class ScoreController : MonoBehaviour
{
    [System.Serializable]
    private class ScoreData { public int score; }

    private ISaveSystem _saveSystem;
    private int _score;

    public int Score => _score;

    // --- Subscription lifecycle (every += has a matching -=) ----------------

    void OnEnable() => AddListeners();
    void OnDisable() => RemoveListeners();

    private void AddListeners()
    {
        GameEvents.TileMerged += HandleTileMerged;
        GameEvents.SaveRequested += HandleSaveRequested;
    }

    private void RemoveListeners()
    {
        GameEvents.TileMerged -= HandleTileMerged;
        GameEvents.SaveRequested -= HandleSaveRequested;
    }

    // --- Wiring -------------------------------------------------------------

    public void Setup(ISaveSystem saveSystem)
    {
        _saveSystem = saveSystem;
        RestoreScore();
    }

    // --- Bus handlers -------------------------------------------------------

    // A tile merged: award points for the new tier. This is the replacement for
    // the old direct call LevelManager -> ScoreController.AddScore(points).
    private void HandleTileMerged(Item merged, Cell cell)
    {
        if (merged == null) return;
        int points = merged.GemData != null ? merged.GemData.scoreValue : merged.Tier * 10;
        AddScore(points);
    }

    // Persist on request. ScoreController raises SaveRequested itself after every
    // change, but routing through the bus means anything else (app pause/quit, a
    // "Save" button) can trigger the same persistence without knowing how we save.
    private void HandleSaveRequested()
    {
        _saveSystem?.Save(new ScoreData { score = _score });
    }

    // --- Score API ----------------------------------------------------------

    public void AddScore(int points)
    {
        _score += points;
        GameEvents.RaiseScoreChanged(_score);
        GameEvents.RaiseSaveRequested();
    }

    public void ResetScore()
    {
        _score = 0;
        GameEvents.RaiseScoreChanged(_score);
        GameEvents.RaiseSaveRequested();
    }

    private void RestoreScore()
    {
        object saved = _saveSystem.Load();
        if (saved is string json)
        {
            ScoreData data = JsonUtility.FromJson<ScoreData>(json);
            if (data != null) _score = data.score;
        }
    }
}
