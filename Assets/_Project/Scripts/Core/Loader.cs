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
