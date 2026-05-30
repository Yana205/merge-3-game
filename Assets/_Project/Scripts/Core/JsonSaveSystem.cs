using System.IO;
using UnityEngine;

public class JsonSaveSystem : ISaveSystem
{
    private readonly string filePath;

    public JsonSaveSystem(string fileName = "save.json")
    {
        filePath = Path.Combine(Application.persistentDataPath, fileName);
    }

    public void Save(object data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(filePath, json);
    }

    public object Load()
    {
        if (!File.Exists(filePath)) return null;
        return File.ReadAllText(filePath);
    }

    public void Clear()
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
