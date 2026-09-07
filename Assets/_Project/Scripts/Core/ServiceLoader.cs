// ADDRESSABLES MIGRATION:
// Migrated asset:   Item.prefab (Assets/_Project/Prefabs/Item.prefab)
// Old load method:  Inspector-wired prefab reference + pool prewarm in ItemPoolManager.Awake
// New key:          "GemItem"
// Group:            Gameplay
//
// SERVICE LOADER DESIGN:
// - ASSETS LOADED:   the GemItem prefab, asynchronously via
//                    Addressables.LoadAssetAsync<GameObject>("GemItem").
// - OBJECTS CREATED: MonoBehaviourPool<Item> (prewarmed with 32 instances of
//                    the loaded prefab), ItemFactory, PlayerPrefsSaveSystem.
// - INJECTIONS:      prefab + pool -> ItemFactory.Init;
//                    factory -> GridManager.SetItemFactory;
//                    save system -> ScoreController.Setup;
//                    separate save system (key "GameProgress") -> ProgressManager.Setup.
// - READINESS:       OnServicesReady fires (and IsReady flips true) only after
//                    every service above is built and injected; LevelManager
//                    waits for it before loading the first level.
//
// WEBGL NOTE — why this is callback-based, not async/await:
// `await handle.Task` NEVER RESOLVES on WebGL (single-threaded; the handle's
// Task is not driven), so the old async version silently left IsReady false
// forever in browser builds — the menu showed, PRESS START faded the screen,
// and no board ever arrived. The Completed callback is fired by Addressables
// itself on the main thread and works on every platform.
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ServiceLoader : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public ScoreController scoreController;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private ProgressManager progressManager;

    // 25-cell default board + merge churn headroom (see POOL DESIGN in ItemFactory.cs).
    private const int PrewarmCount = 32;

    // Fired once every service is created and injected.
    public event System.Action OnServicesReady;

    // True after OnServicesReady fired, for late subscribers.
    public bool IsReady { get; private set; }

    private ItemFactory _itemFactory;

    void Start()
    {
        // Save system -> ScoreController (synchronous wiring, kept from Loader).
        if (scoreController == null)
        {
            Debug.LogError("ServiceLoader: scoreController is not assigned.");
        }
        else
        {
            ISaveSystem saveSystem = new PlayerPrefsSaveSystem();
            scoreController.Setup(saveSystem);
        }

        progressManager?.Setup(new PlayerPrefsSaveSystem("GameProgress"));

        Addressables.LoadAssetAsync<GameObject>("GemItem").Completed += HandleGemItemLoaded;
    }

    private void HandleGemItemLoaded(AsyncOperationHandle<GameObject> handle)
    {
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogError("ServiceLoader: GemItem load failed — " +
                (handle.OperationException != null ? handle.OperationException.Message : "no exception info"));
            return;
        }

        var pool = new MonoBehaviourPool<Item>();
        pool.Init(handle.Result, PrewarmCount);

        _itemFactory = new ItemFactory();
        _itemFactory.Init(handle.Result, pool);

        if (gridManager == null)
            Debug.LogError("ServiceLoader: gridManager is not assigned — ItemFactory not injected.");
        else
            gridManager.SetItemFactory(_itemFactory);

        IsReady = true;
        // Logged on purpose: this line in a browser console is the proof that the
        // WebGL build got past the Addressables load (see WEBGL NOTE above).
        Debug.Log("ServiceLoader: services ready.");
        OnServicesReady?.Invoke();
    }
}
