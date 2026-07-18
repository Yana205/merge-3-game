using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectUI : MonoBehaviour
{
    public void PlayGame()
    {
        LoadLevel(0);
    }

    public void LoadLevel(int levelIndex)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadGame(levelIndex);
        else
        {
            PlayerPrefs.SetInt("SelectedLevel", levelIndex);
            // Reload the currently active scene — its name is not "Game",
            // and hardcoding it breaks if the scene is ever renamed.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void QuitGame()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.QuitGame();
        else
            Application.Quit();
    }
}
