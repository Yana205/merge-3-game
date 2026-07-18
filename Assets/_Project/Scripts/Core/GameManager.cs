using UnityEngine;
using UnityEngine.SceneManagement;

// ARCHITECTURE (see docs/ARCHITECTURE.md for the full diagram):
//   1. CONTROLLER  — ServiceLoader / GameManager: startup + injection,
//                    holds only manager references.
//        │ injects factory / save system; signals OnServicesReady
//   2. MANAGERS    — LevelManager, GridManager, MergeManager, InputHandler,
//                    UIManager, MenuController: all game rules.
//        │ factory.Get() / factory.Release() — never Instantiate
//   3. FACTORY     — ItemFactory: one Init(prefab, pool); no game logic.
//        │ pool.Get() / pool.Release(); Addressables handle.Result
//   4. SERVICES    — MonoBehaviourPool<Item>, Addressables, ISaveSystem,
//                    GemConfig / LevelData ScriptableObjects.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Single-scene project: both flows reload the active scene rather than
    // hardcoding scene names ("Game"/"MainMenu" don't exist in the build).
    public void LoadMainMenu()
    {
        MenuController.ShowMenuOnNextLoad();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadGame(int levelIndex)
    {
        PlayerPrefs.SetInt("SelectedLevel", levelIndex);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
