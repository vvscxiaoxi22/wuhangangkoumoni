using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "故障信息", menuName = "Data/故障信息")]
public class 故障信息 : ScriptableObject
{
    [SerializedDictionary("ID", "内容")]
    public SerializedDictionary<int, 故障信息结构> _故障;
}
[System.Serializable]
public class 故障信息结构
{
    public string 故障现象;
    public string 故障点;
    public string 标号;
    public string 类型;
}

