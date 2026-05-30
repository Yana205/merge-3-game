using UnityEngine;

public class PlayerPrefsSaveSystem : ISaveSystem
{
    private readonly string key;

    public PlayerPrefsSaveSystem(string prefsKey = "SaveData")
    {
        key = prefsKey;
    }

    public void Save(object data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    public object Load()
    {
        if (!PlayerPrefs.HasKey(key)) return null;
        return PlayerPrefs.GetString(key);
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
