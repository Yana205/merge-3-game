using UnityEngine;

/// <summary>
/// The run's difficulty curve, driven purely by score. Each stage brings one more
/// gem colour into play until the palette is full, then keeps tightening the
/// screws with stones. It listens to <see cref="GameEvents.ScoreChanged"/> (so a
/// run reset to 0 drops it back to stage 0 with no extra wiring), pushes the
/// current rules into <see cref="MergeManager"/>, and announces every change on
/// the bus for the HUD, the juice and the audio.
/// </summary>
public class StageDirector : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    [Tooltip("Where the colour count and stone pressure are pushed. Auto-found if unset.")]
    [SerializeField] private MergeManager mergeManager;

    [Header("Stages")]
    [Tooltip("Score at which each stage begins. Stage 0 is the run start. Beyond " +
             "the last entry, stages keep coming every 'lateStageStep' points.")]
    [SerializeField] private int[] stageStarts = { 0, 1500, 4000, 8000, 13000, 19000, 26000 };

    [Tooltip("Points per stage once the table above runs out.")]
    [SerializeField] private int lateStageStep = 8000;

    [Header("Colours")]
    [Tooltip("Gem colours in play at stage 0. Each stage adds one until the palette is full.")]
    [Range(3, 6)]
    [SerializeField] private int startingColors = 4;

    [Header("Stones")]
    [SerializeField] private float stoneChanceStart = 0.05f;
    [SerializeField] private float stoneChancePerStage = 0.02f;
    [SerializeField] private float stoneChanceMax = 0.16f;
    [SerializeField] private int maxStonesStart = 3;
    [SerializeField] private int maxStonesCap = 6;
    [Tooltip("From this stage on, fresh stones take three hits instead of two.")]
    [SerializeField] private int toughStonesFromStage = 4;

    /// <summary>Current stage index (0-based). -1 until the first score arrives.</summary>
    public int Stage { get; private set; } = -1;

    public int ColorsFor(int stage) => Mathf.Min(GemPalette.Count, startingColors + Mathf.Max(0, stage));

    void Awake()
    {
        if (mergeManager == null)
            mergeManager = FindAnyObjectByType<MergeManager>();
        if (mergeManager == null)
            Debug.LogError("StageDirector: no MergeManager in the scene — progression cannot apply.");
    }

    void OnEnable() => GameEvents.ScoreChanged += HandleScoreChanged;
    void OnDisable() => GameEvents.ScoreChanged -= HandleScoreChanged;

    private void HandleScoreChanged(int total)
    {
        int stage = StageFor(total);
        if (stage == Stage) return;

        int previous = Stage;
        Stage = stage;
        Apply(stage);

        GameEvents.RaiseStageChanged(stage, ColorsFor(stage), StartOf(stage), StartOf(stage + 1));

        // A real stage-UP (not the reset to 0 at run start) that widened the
        // palette is worth a fanfare and a name.
        if (previous >= 0 && stage > previous && total > 0 && ColorsFor(stage) > ColorsFor(previous))
            GameEvents.RaiseColorUnlocked(ColorsFor(stage));
    }

    private void Apply(int stage)
    {
        if (mergeManager == null) return;
        mergeManager.SetActiveColors(ColorsFor(stage));

        // Stones only start pressing once the palette is full — one new thing at a time.
        int stoneStage = Mathf.Max(0, stage - (GemPalette.Count - startingColors));
        float chance = Mathf.Min(stoneChanceMax, stoneChanceStart + stoneChancePerStage * stoneStage);
        int maxOnBoard = Mathf.Min(maxStonesCap, maxStonesStart + stoneStage / 2);
        int hp = stage >= toughStonesFromStage ? 3 : 2;
        mergeManager.SetStonePressure(chance, maxOnBoard, hp);
    }

    public int StageFor(int score)
    {
        if (stageStarts == null || stageStarts.Length == 0) return 0;
        int stage = 0;
        for (int i = 0; i < stageStarts.Length; i++)
            if (score >= stageStarts[i]) stage = i;
        int last = stageStarts[stageStarts.Length - 1];
        if (score >= last && lateStageStep > 0)
            stage = stageStarts.Length - 1 + (score - last) / lateStageStep;
        return stage;
    }

    public int StartOf(int stage)
    {
        if (stageStarts == null || stageStarts.Length == 0) return 0;
        if (stage < stageStarts.Length) return stageStarts[Mathf.Max(0, stage)];
        return stageStarts[stageStarts.Length - 1] + (stage - (stageStarts.Length - 1)) * lateStageStep;
    }
}
