using System.Collections.Generic;
using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    public const int MaxLeaderboardEntries = 10;

    // Where the HUD used to keep its own separate best score. Read once at
    // startup for migration, then removed. See MigrateLegacyHighScore.
    private const string LegacyHighScoreKey = "Merge3_HighScore";

    private ISaveSystem _saveSystem;
    private GameProgress _progress = new GameProgress();

    public void Setup(ISaveSystem saveSystem)
    {
        _saveSystem = saveSystem;
        RestoreProgress();
    }

    // --- Endless mode leaderboard --------------------------------------------

    // The run in progress owns one leaderboard row, which is rewritten as the
    // score climbs instead of a new row being appended. Held by reference so the
    // row can be found again after each re-sort.
    private RunEntry _currentRun;

    /// <summary>
    /// Start a new run's leaderboard entry. The next <see cref="SubmitRunScore"/>
    /// opens a fresh row rather than growing the previous run's.
    /// </summary>
    public void BeginRun()
    {
        _currentRun = null;
    }

    /// <summary>
    /// Update the in-progress run's row. Safe to call on every single score
    /// change — it rewrites one row rather than appending.
    /// </summary>
    /// <remarks>
    /// This is what makes the leaderboard survive a refresh. A run used to reach
    /// the board only via <see cref="RecordRun"/> on game over, so quitting or
    /// reloading mid-run lost it — while the HUD's separate best score kept it,
    /// which is how a saved best could sit above an empty leaderboard.
    /// </remarks>
    public void SubmitRunScore(int score)
    {
        if (score <= 0) return;
        if (_currentRun != null && _currentRun.score == score) return;

        var runs = new List<RunEntry>(_progress.topRuns ?? new RunEntry[0]);

        if (_currentRun == null)
        {
            _currentRun = new RunEntry { score = score };
            runs.Add(_currentRun);
        }
        else
        {
            _currentRun.score = score;
            // It may have been trimmed off the bottom while it was small.
            if (!runs.Contains(_currentRun)) runs.Add(_currentRun);
        }

        SortTrimAndSave(runs);
    }

    /// <summary>
    /// Finalise a finished run. Score is the only thing a run is measured by now
    /// that the board is one continuous grid — <see cref="RunEntry.level"/> stays
    /// in the saved type so older save files still deserialize, but is not written.
    /// </summary>
    public void RecordRun(int score)
    {
        SubmitRunScore(score);
        _currentRun = null;   // over; the next run opens its own row
    }

    private void SortTrimAndSave(List<RunEntry> runs)
    {
        runs.Sort((a, b) => b.score.CompareTo(a.score));
        if (runs.Count > MaxLeaderboardEntries)
            runs.RemoveRange(MaxLeaderboardEntries, runs.Count - MaxLeaderboardEntries);

        _progress.topRuns = runs.ToArray();
        _saveSystem?.Save(_progress);
    }

    public IReadOnlyList<RunEntry> GetTopRuns()
    {
        return _progress.topRuns ?? new RunEntry[0];
    }

    /// <summary>Best run score so far (0 when no runs recorded), for menu/HUD.</summary>
    public int GetBestScore()
    {
        int best = 0;
        if (_progress.topRuns != null)
            foreach (RunEntry r in _progress.topRuns)
                if (r.score > best) best = r.score;
        return best;
    }

    public bool IsUnlocked(int levelIndex)
    {
        if (levelIndex <= 0) return true;
        return IsCompleted(levelIndex - 1);
    }

    public bool IsCompleted(int levelIndex)
    {
        return levelIndex >= 0 && levelIndex < _progress.completed.Length && _progress.completed[levelIndex];
    }

    public int GetBestScore(int levelIndex)
    {
        return levelIndex >= 0 && levelIndex < _progress.bestScores.Length ? _progress.bestScores[levelIndex] : 0;
    }

    public void RecordResult(int levelIndex, int score)
    {
        if (levelIndex < 0) return;

        EnsureSize(levelIndex + 1);
        _progress.completed[levelIndex] = true;
        if (score > _progress.bestScores[levelIndex])
            _progress.bestScores[levelIndex] = score;

        _saveSystem?.Save(_progress);
    }

    private void EnsureSize(int minLength)
    {
        if (_progress.bestScores.Length >= minLength) return;

        var bestScores = new int[minLength];
        var completed = new bool[minLength];
        _progress.bestScores.CopyTo(bestScores, 0);
        _progress.completed.CopyTo(completed, 0);
        _progress.bestScores = bestScores;
        _progress.completed = completed;
    }

    private void RestoreProgress()
    {
        object saved = _saveSystem.Load();
        if (saved is string json)
        {
            GameProgress data = JsonUtility.FromJson<GameProgress>(json);
            if (data != null)
                _progress = data;
        }

        MigrateLegacyHighScore();
    }

    // The HUD used to keep its own best score under this PlayerPrefs key,
    // entirely separate from this leaderboard — which is how a player could end
    // up with a saved best sitting above an empty board. Fold any existing value
    // in once so their record survives the switch to a single store.
    private void MigrateLegacyHighScore()
    {
        int legacy = PlayerPrefs.GetInt(LegacyHighScoreKey, 0);
        if (legacy <= 0) return;

        if (legacy > GetBestScore())
        {
            var runs = new List<RunEntry>(_progress.topRuns ?? new RunEntry[0]);
            runs.Add(new RunEntry { score = legacy });
            SortTrimAndSave(runs);
        }

        PlayerPrefs.DeleteKey(LegacyHighScoreKey);
        PlayerPrefs.Save();
    }
}
