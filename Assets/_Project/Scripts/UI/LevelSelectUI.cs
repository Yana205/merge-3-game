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
            SceneManager.LoadScene("Game");
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
