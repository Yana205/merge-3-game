using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    private ISaveSystem _saveSystem;
    private GameProgress _progress = new GameProgress();

    public void Setup(ISaveSystem saveSystem)
    {
        _saveSystem = saveSystem;
        RestoreProgress();
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
