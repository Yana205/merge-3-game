using System.Collections.Generic;
using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    public const int MaxLeaderboardEntries = 10;

    private ISaveSystem _saveSystem;
    private GameProgress _progress = new GameProgress();

    public void Setup(ISaveSystem saveSystem)
    {
        _saveSystem = saveSystem;
        RestoreProgress();
    }

    // --- Endless mode leaderboard --------------------------------------------

    /// <summary>
    /// Record a finished run, keeping only the top <see cref="MaxLeaderboardEntries"/>
    /// by score. Ties break toward the higher level reached.
    /// </summary>
    public void RecordRun(int score, int level)
    {
        var runs = new List<RunEntry>(_progress.topRuns ?? new RunEntry[0]);
        runs.Add(new RunEntry { score = score, level = level });
        runs.Sort((a, b) => a.score != b.score
            ? b.score.CompareTo(a.score)
            : b.level.CompareTo(a.level));
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
    }
}
