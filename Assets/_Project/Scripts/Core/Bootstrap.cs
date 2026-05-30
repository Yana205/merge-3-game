using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadMainMenu();
    }
}
