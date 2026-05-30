using UnityEngine;

public class Loader : MonoBehaviour
{
    public ScoreController scoreController;

    void Awake()
    {
        ISaveSystem saveSystem = new JsonSaveSystem();
        scoreController.Setup(saveSystem);
    }
}
