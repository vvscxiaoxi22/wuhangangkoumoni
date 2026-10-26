

using UnityEngine;
using System.Collections.Generic;


#if (UNITY_EDITOR && DEBUG)
using System.Diagnostics;
#endif
////计时器委托
public delegate void Callback();
public delegate void ArgCallback(object args);
////public delegate void GTCallback<T>(T arg);

public class EasyTimer : MonoBehaviour
{

    private static GameObject m_MainObject = null;//用于全局管理的游戏对象

    private static List<TimerHandle> m_Active = new List<TimerHandle>();//保存所有处于激活状态的计时器
    private static List<TimerHandle> m_Pool;//对象池，避免反复创建对象

    private static TimerHandle m_NewTimerHandle = null;

    private static int m_TimerHandleCount = 0;//当前激活的事件的数量

    public static int PoolVolume = 200; //对象池最大容量

    public float _time = 0;
    public struct Stats
    {
        public int Created;
        public int Inactive;
        public int Active;
    }



    //当不正确的添加 EasyTimer 的时候编辑器会提示
    public bool WasAddedCorrectly
    {
        get
        {
            if (!Application.isPlaying)
                return false;
            if (gameObject != m_MainObject)
                return false;
            return true;
        }
    }



    //当不正确的添加 easyTimer 的时候销毁自身
    private void Awake()
    {

        if (!WasAddedCorrectly)
        {
            Destroy(this);
            return;
        }

    }


    private void Update()
    {
        for (int i = m_Active.Count -1; i >= 0; --i)
        {
            _time = m_Active[i].IsEgnoreTimeScale ? Time.unscaledTime : Time.time;
            if (_time >= m_Active[i].DueTime)
            {
                m_Active[i].Execute();
            }
            else
            {
                m_Active[i].DelayTime();
            }
        }

    }
    /// <summary>
    /// 创建计时器（将永远执行下去）
    /// </summary>
    /// <param name="delay">首次延迟</param>
    /// <param name="callback">回调函数</param>
    /// <returns></returns>
    public static TimerHandle CreateTimer(float delay, Callback callback)
    {
        return NewTimer(delay,1, -1, callback, null, null);
    }
    /// <summary>
    /// 创建计时器
    /// </summary>
    /// <param name="delay">首次延迟</param>
    /// <param name="iterations">执行总次数 0 代表无限次数</param>
    /// <param name="callback">回调函数</param>
    /// <returns></returns>
    public static TimerHandle CreateTimer(float delay, int iterations, Callback callback)
    {
        return NewTimer(delay, iterations, -1, callback, null, null);
    }
    /// <summary>
    /// 创建计时器
    /// </summary>
    /// <param name="delay">首次延迟</param>
    /// <param name="iterations">执行总次数 0 代表无限次数</param>
    /// <param name="interval">第二次及之后执行的延迟</param>
    /// <param name="callback">回调函数</param>
    /// <returns></returns>
    public static TimerHandle CreateTimer(float delay, int iterations, float interval, Callback callback)
    {
        return NewTimer(delay, iterations, interval, callback, null, null);
    }
    /// <summary>
    /// 创建计时器（将永远执行下去）
    /// </summary>
    /// <param name="delay">首次延迟</param>
    /// <param name="callback">回调函数</param>
    /// <param name="args">回调函数的参数</param>
    /// <returns></returns>
    public static TimerHandle CreateTimer(float delay, ArgCallback callback, Object args)
    {
        return NewTimer(delay, 1, -1, null, callback, args);
    }
    /// <summary>
    /// 创建计时器
    /// </summary>
    /// <param name="delay">首次延迟</param>
    /// <param name="iterations">执行总次数 0 代表无限次数</param>
    /// <param name="callback">回调函数</param>
    /// <param name="args">回调函数的参数</param>
    /// <returns></returns>
    public static TimerHandle CreateTimer(float delay, int iterations, ArgCallback callback, Object args)
    {
        return NewTimer(delay, iterations, -1, null, callback, args);
    }
    /// <summary>
    /// 创建计时器
    /// </summary>
    /// <param name="delay">首次延迟</param>
    /// <param name="iterations">执行总次数 0 代表无限次数</param>
    /// <param name="interval">第二次及之后执行的延迟</param>
    /// <param name="callback">回调函数</param>
    /// <param name="args">回调函数的参数</param>
    /// <returns></returns>
    public static TimerHandle CreateTimer(float delay, int iterations, float interval, ArgCallback callback, Object args)
    {
        return NewTimer(delay, iterations, interval, null, callback, args);
    }

    //初使化计时器，并将初始化后的m_NewTimerHandle对象放入 m_Active 集合中
    private static TimerHandle NewTimer(float delay, int iterations, float interval, Callback callback, ArgCallback argCallback, object arguments)
    {
        if (callback == null && argCallback == null)
        {
            UnityEngine.Debug.LogError("Error: 创建计时器失败-->没有注册回调函数 .");
            return null;
        }

        if (m_MainObject == null)
        {
            m_MainObject = new GameObject("[Timers]");
            m_MainObject.AddComponent<EasyTimer>();
            UnityEngine.Object.DontDestroyOnLoad(m_MainObject);
        }
        if (m_Pool == null)
        {
            m_Pool = new List<TimerHandle>(PoolVolume);
        }

        delay = Mathf.Max(0.0f, delay);
        iterations = Mathf.Max(0, iterations);
        interval = (interval == -1.0f) ? delay : Mathf.Max(0.0f, interval);

        m_NewTimerHandle = null;
        if (m_Pool.Count > 0)
        {
            m_NewTimerHandle = m_Pool[0];
            m_NewTimerHandle.ResetTimer(delay, iterations, interval, callback, argCallback, arguments, ++m_TimerHandleCount);
            m_Pool.RemoveAt(0);
        }
        else
            m_NewTimerHandle = new TimerHandle(delay, iterations, interval, callback, argCallback, arguments, ++m_TimerHandleCount);

        EasyTimer.m_Active.Add(m_NewTimerHandle);

#if (UNITY_EDITOR)
        m_NewTimerHandle.StoreCallingMethod();
        EditorRefresh();
#endif

        m_NewTimerHandle = null;
        return m_Active[m_TimerHandleCount -1];

    }
    /// <summary>
    ///取消所有计时器（放入对象池） 
    /// </summary>
    public static void CancelAll()
    {

        for (int i = EasyTimer.m_Active.Count - 1; i > -1; --i)
        {
            m_Active[i].Cancel();
        }
#if (UNITY_EDITOR)
        EditorRefresh();
#endif
    }
    /// <summary>
    /// 暂停所有计时器
    /// </summary>
    public static void PauseAll()
    {
        for (int i = m_Active.Count - 1; i> -1;-- i)
        {
            if (!m_Active[i].Paused)
            {
                m_Active[i].Paused = true;
            }
        }
    }
    
    /// <summary>
    /// 重启所有计时器
    /// </summary>
    public static void ResumeAll()
    {
        for (int i = m_Active.Count - 1; i > -1; --i)
        {
            if (m_Active[i].Paused)
            {
                m_Active[i].Paused = false;
            }
        }
    }

    /// <summary>
    /// 销毁计时器
    /// </summary>
    public static void DestroyAll()
    {
        EasyTimer.m_Active.Clear();
        EasyTimer.m_Pool.Clear();
        m_NewTimerHandle = null;
        m_TimerHandleCount = 0;
#if (UNITY_EDITOR && DEBUG)
        EditorRefresh();
#endif

    }

    public static void Pasue(int id)
    {
        if( id > 0 && id <= m_Active.Count)
        {
            m_Active[id].Paused = true;
        }
    }
    /// <summary>
    /// 暂停计时器
    /// </summary>
    /// <param name="id"></param>
    public static void Resume(int id )
    {
        if (id > 0 && id <= m_Active.Count)
        {
            m_Active[id].Paused = false;
        }
    }
    /// <summary>
    /// 取消计时器
    /// </summary>
    /// <param name="id"></param>
    public static void Cancel(int id )
    {
        if (id > 0 && id <= m_Active.Count)
        {
            m_Active[id].Cancel();
        }
    }
    /// <summary>
    /// 销毁计时器
    /// </summary>
    /// <param name="id"></param>
    public static void Destroy(int id )
    {
        if (id > 0 && id <= m_Active.Count)
        {
            m_Active.RemoveAt(id);
        }
    }
    /// <summary>
    /// 销毁计时器
    /// </summary>
    /// <param name="handle"></param>
    public static void Destroy(TimerHandle handle)
    {
        m_Active.Remove(handle);
        m_Pool.Remove(handle);
    }
    /// <summary>
    /// 清空缓存池
    /// </summary>
    public static void ClearPool()
    {
        m_Pool.Clear();
    }

    public static void PushPool(TimerHandle handle)
    {
        if(m_Active.Remove(handle))
        {
            if (m_Pool.Count < PoolVolume)
                m_Pool.Add(handle);

            --m_TimerHandleCount;
        }
    }
    /// <summary>
    /// 获取当前激活状态的计时器数量
    /// </summary>
    /// <returns></returns>
    public static int GetActiveTimerCount()
    {
        return m_TimerHandleCount;
    }
    public static Stats EditorGetStats()
    {

        Stats stats;
        stats.Created = m_Active.Count + m_Pool.Count;
        stats.Inactive = m_Pool.Count;
        stats.Active = m_Active.Count;
        return stats;

    }

    public static string EditorGetMethodInfo(int TimerHandleIndex)
    {

        if (TimerHandleIndex < 0 || TimerHandleIndex > m_Active.Count - 1)
            return "索引超出范围.";

        return m_Active[TimerHandleIndex].MethodInfo;

    }

    public static int EditorGetMethodId(int TimerHandleIndex)
    {

        if (TimerHandleIndex < 0 || TimerHandleIndex > m_Active.Count - 1)
            return 0;

        return m_Active[TimerHandleIndex].Id;

    }

#if (UNITY_EDITOR)

    public static void EditorRefresh()
    {
        m_MainObject.name = "[Timers (" + m_Active.Count + " / " + (m_Pool.Count + m_Active.Count).ToString() + ")]";
    }
#endif

}


