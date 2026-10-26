using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "任务信息", menuName = "Data/任务信息")]
public class 任务信息 : ScriptableObject
{
    [SerializedDictionary("任务名称", "内容")]
    public SerializedDictionary<string, 任务信息结构> _信息;
}
[System.Serializable]
public class 任务信息结构
{
    public string 任务类型;
    [TextArea(1, 5)]
    public string 任务描述;
    [SerializedDictionary("任务名称", "内容")]
    public SerializedDictionary<string, List<string>> _目标;
}