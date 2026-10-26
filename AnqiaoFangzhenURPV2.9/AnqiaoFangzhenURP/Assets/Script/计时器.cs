using UnityEngine;

public class 计时器 : MonoBehaviour
{
    #region 单例
    private static 计时器 _Instance;
    public static 计时器 Get
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindFirstObjectByType(typeof(计时器)) as 计时器;
            }
            return _Instance;
        }
    }
    #endregion
    public bool isRunning;      // 是否启动计时

    void Update()
    {
        if (isRunning)
        {
            岸桥StateData.Get._任务State.用时 += Time.deltaTime;
        }
    }

    // 开始计时
    public void StartTimer()
    {
        岸桥StateData.Get._任务State.用时 = 0;
        isRunning = true;
    }

    // 暂停计时
    public void PauseTimer()
    {
        isRunning = false;
    }

    // 继续计时
    public void ResumeTimer()
    {
        isRunning = true;
    }

    // 重置清零
    public void ResetTimer()
    {
        岸桥StateData.Get._任务State.用时 = 0;
        isRunning = false;
    }

    // 格式化输出：00:00:00
    public string GetTimeString()
    {
        int hour = Mathf.FloorToInt(岸桥StateData.Get._任务State.用时 / 3600);
        int minute = Mathf.FloorToInt((岸桥StateData.Get._任务State.用时 % 3600) / 60);
        int second = Mathf.FloorToInt(岸桥StateData.Get._任务State.用时 % 60);
        return $"{hour:D2}:{minute:D2}:{second:D2}";
    }
}
