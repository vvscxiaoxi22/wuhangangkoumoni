using UnityEngine;
using UnityEngine.Events;

public class 岸桥Control : MonoBehaviour
{
    [Header("挡位(float)")]
    public UnityEvent<float>
    大车移动Control,
    小车移动Control,
    吊具升降Control,
    吊具旋转Control,
    吊具前后倾斜Control,
    吊具左右倾斜Control,
    导板全部上下Control;

    [Header("扳机(Int)")]
    public UnityEvent<int>
    海左导板上下Control,
    海右导板上下Control,
    陆左导板上下Control,
    陆右导板上下Control,
    吊具开闭锁Control,
    单双箱选择Control;

    [Header("按钮")]
    public UnityEvent
    吊具回零Control,
    吊具泵开Control,
    吊具泵关Control,
    吊具20英尺Control,
    吊具40英尺Control,
    大车鸣笛Control,
    控制合Control,
    控制关Control,
    夹轮Control,
    松轮Control;
}
