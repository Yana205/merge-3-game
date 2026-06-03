using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Merge Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Grid Size")]
    public int rows = 5;
    public int cols = 5;

    [Header("Win Condition")]
    public int targetScore = 100;

    // FUTURE: add time limit, move limit, star thresholds
    // FUTURE: add allowed tier range (e.g., only merge up to tier 3)
}
