using UnityEngine;

public class ScoreController : MonoBehaviour
{
    [System.Serializable]
    private class ScoreData { public int score; }

    private ISaveSystem _saveSystem;
    private int _score;

    public int Score => _score;

    public void Setup(ISaveSystem saveSystem)
    {
        _saveSystem = saveSystem;
        RestoreScore();
    }

    public void AddScore(int points)
    {
        _score += points;
        _saveSystem?.Save(new ScoreData { score = _score });
    }

    public void ResetScore()
    {
        _score = 0;
        _saveSystem?.Save(new ScoreData { score = _score });
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
