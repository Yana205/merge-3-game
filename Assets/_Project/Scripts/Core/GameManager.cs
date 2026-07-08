using UnityEngine;
using UnityEngine.SceneManagement;

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
