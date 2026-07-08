// ADDRESSABLES MIGRATION:
// Migrated asset:   Item.prefab (Assets/_Project/Prefabs/Item.prefab)
// Old load method:  Inspector-wired prefab reference + pool prewarm in ItemPoolManager.Awake
// New key:          "GemItem"
// Group:            Gameplay
using UnityEngine;

public class Loader : MonoBehaviour
{
    public ScoreController scoreController;

    void Awake()
    {
        ISaveSystem saveSystem = new PlayerPrefsSaveSystem();
        scoreController.Setup(saveSystem);
    }
}
