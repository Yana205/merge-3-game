using UnityEngine;

[System.Serializable]
public struct SpawnEntry
{
    public int tier;
    public float weight;
}

[CreateAssetMenu(fileName = "NewLevel", menuName = "Merge Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Grid Size")]
    public int rows = 5;
    public int cols = 5;

    [Header("Win Condition")]
    public int targetScore = 100;

    [Header("Spawn Config")]
    public SpawnEntry[] spawnTable = new SpawnEntry[] { new SpawnEntry { tier = 1, weight = 1f } };
    [Range(0f, 1f)] public float emptyChance = 0f;
    [Min(0)] public int guaranteedPairs = 1;

    public int PickRandomTier()
    {
        if (spawnTable == null || spawnTable.Length == 0)
            return 1;

        float totalWeight = 0f;
        foreach (var entry in spawnTable)
            totalWeight += entry.weight;

        float roll = Random.Range(0f, totalWeight);
        float accumulated = 0f;
        foreach (var entry in spawnTable)
        {
            accumulated += entry.weight;
            if (accumulated > roll)
                return entry.tier;
        }

        return 1;
    }
}
