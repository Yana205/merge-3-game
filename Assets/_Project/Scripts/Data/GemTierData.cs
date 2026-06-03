using UnityEngine;

[System.Serializable]
public class GemTierData
{
    [Header("Identity")]
    public string gemName;
    public float hardness;

    [Header("Visuals")]
    public Color tintColor;
    public Sprite sprite;

    [Header("Scoring")]
    public int scoreValue;
}
