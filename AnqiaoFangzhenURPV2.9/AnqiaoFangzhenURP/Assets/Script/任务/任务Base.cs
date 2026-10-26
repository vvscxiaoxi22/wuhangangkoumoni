using AYellowpaper.SerializedCollections;
using UnityEngine;

public class 任务Base : MonoBehaviour
{
    [SerializedDictionary("控制开关", "物体")]
    public SerializedDictionary<GameObject, bool> _控制开关;
    private void Start()
    {
        初始化();
    }
    public virtual void 初始化()
    {
        foreach (var item in _控制开关)
        {
            item.Key.SetActive(item.Value);
        }
    }
    public virtual void 任务完成()
    {
        任务Manager.Get.完成任务(); 
    }
    public virtual void 任务失败()
    {

    }
}
