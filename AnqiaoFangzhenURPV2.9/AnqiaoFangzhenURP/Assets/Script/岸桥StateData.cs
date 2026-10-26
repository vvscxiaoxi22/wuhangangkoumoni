using UnityEngine;

public class 岸桥StateData : MonoBehaviour
{
    #region 单例
    private static 岸桥StateData _Instance;
    public static 岸桥StateData Get
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindFirstObjectByType(typeof(岸桥StateData)) as 岸桥StateData;
            }
            return _Instance;
        }
    }
    #endregion
    public 岸桥StateCreat _岸桥State;
    public 任务StateCreat _任务State;
    public 任务信息 _任务信息;
    public 控制字段 _控制字段;
}
