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
//                    save system -> ScoreController.Setup.
// - READINESS:       OnServicesReady fires (and IsReady flips true) only after
//                    every service above is built and injected; LevelManager
//                    waits for it before loading the first level.
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ServiceLoader : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public ScoreController scoreController;
    [SerializeField] private GridManager gridManager;

    // 25-cell default board + merge churn headroom (see ItemPoolManager notes).
    private const int PrewarmCount = 32;

    // Fired once every service is created and injected.
    public event System.Action OnServicesReady;

    // True after OnServicesReady fired, for late subscribers.
    public bool IsReady { get; private set; }

    private ItemFactory _itemFactory;

    void Start() => _ = LoadAsync();

    public async Task LoadAsync()
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

        var handle = Addressables.LoadAssetAsync<GameObject>("GemItem");
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError(handle.OperationException != null
                ? handle.OperationException.Message
                : "GemItem load failed");
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
        OnServicesReady?.Invoke();
    }
}
