[System.Serializable]
public class GameProgress
{
    // Legacy per-level fields — kept so old saves still deserialize cleanly.
    public int[] bestScores = new int[0];
    public bool[] completed = new bool[0];

    // Endless mode leaderboard: the best runs, each with the level reached.
    public RunEntry[] topRuns = new RunEntry[0];
}

[System.Serializable]
public class RunEntry
{
    public int score;
    public int level;
}
