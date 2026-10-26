using UnityEngine;
using System;

#if (UNITY_EDITOR && DEBUG)
using System.Diagnostics;
#endif

public class TimerHandle
{


    private Callback Function = null;
    private ArgCallback ArgFunc = null;
    private object arguments = null;



    private bool paused = false;
    private float startTime = 0;
    private float dueTime = 0;
    private int totalIterations = 0;
    private float firstInterval = 0;
    private float interval = 0;
    private bool isActive = true;
    private int id = 0;

    private int executeIterations = 0;
    public float currentExecuteTime = 0;
    private bool isEgnoreTimeScale;

    #region Property

    /// <summary>
    /// 计时器id
    /// </summary>
    public int Id
    {
        get
        {
            if (isActive)
                return this.id;
            return 0;
        }
    }
    /// <summary>
    /// 计时器启动时间
    /// </summary>
    public float StartTime
    {
        get
        {
            if (isActive)
                return startTime;
            return 0;
        }
    }
    /// <summary>
    /// 下一次执行回调时间
    /// </summary>
    public float DueTime
    {
        get
        {
            if (isActive)
                return this.dueTime;
            return 0;
        }
    }
    /// <summary>
    /// 上一次执行回调的时间
    /// </summary>
    public float LastDueTime
    {
        get
        {
            if (isActive)
                return currentExecuteTime;
            return 0;
        }
    }
    /// <summary>
    /// 计时器要执行的总次数，0 代表无限次数
    /// </summary>
    public int TotalIterations
    {
        get
        {
            if (isActive )
                return totalIterations;
            return -1;
        }
    }
    /// <summary>
    /// 计时器是否无限执行
    /// </summary>
    public bool IsEndless
    {
        get
        {
            if (isActive)
                if (totalIterations <= 0)
                    return true;
            return false;
        }
    }
    /// <summary>
    /// 计时器当前执行的次数
    /// </summary>
    public int CurrentIterations
    {
        get
        {
            if (isActive)
                return executeIterations;
            return 0;
        }
    }
    /// <summary>
    /// 计时器剩余执行次数， -1 代表无限次
    /// </summary>
    public int LeftIterations
    {
        get
        {
            if (isActive)
                if (totalIterations <= 0)
                    return -1;
                else
                    return totalIterations - executeIterations;
            return 0;
        }
    }
    /// <summary>
    /// 第一次执行回调的间隔时间
    /// </summary>
    public float FirstInterval
    {
        get
        {
            if (isActive)
                return firstInterval;
            return 0;
        }
    }
    /// <summary>
    /// 第二次及以后回调的间隔时间
    /// </summary>
    public float NextInterval
    {
        get
        {
            if (isActive)
                return interval;
            return 0;
        }
    }
    /// <summary>
    /// 计时器的状态设置（运行或暂停）
    /// </summary>
    public bool Paused
    {
        get
        {
            return isActive && this.paused;
        }
        set
        {
            if (isActive)
            {
                this.paused = value;
            }
        }
    }
    /// <summary>
    /// 计时器运行总时长
    /// </summary>
    public float LifeTime
    {
        get
        {
            if (isActive)
                return (isEgnoreTimeScale?Time.unscaledTime:Time.time) + firstInterval + executeIterations * interval - dueTime;

            return 0;
        }
    }
    /// <summary>
    /// 距离下一次执行回调的剩余时间
    /// </summary>
    public float TimeOfTillNextExecute
    {
        get
        {
            if (isActive)
            {
                if (dueTime == 0)
                    return 0;
                return dueTime - (isEgnoreTimeScale ? Time.unscaledTime : Time.time);
            }
            return 0;
        }
    }
    /// <summary>
    /// 距离上一次执行回调的时间间隔
    /// </summary>
    public float TimeOfTillLastExecute
    {
        get
        {
            if (isActive)
                if (executeIterations == 0)
                    return -1;
                else
                    return (isEgnoreTimeScale ? Time.unscaledTime : Time.time) - currentExecuteTime;
            return 0;
        }
    }
    /// <summary>
    /// 是否是激活状态
    /// </summary>
    public bool IsActive
    {
        get
        {
            return isActive;
        }
    }
    /// <summary>
    /// 是否忽略时间缩放
    /// </summary>
    public bool IsEgnoreTimeScale
    {
        get
        {
            if(IsActive)
            {
                return isEgnoreTimeScale;
            }
            return false;
        }
    }
    /// <summary>
    /// 计时器暂停的总时间
    /// </summary>
    public float PausedTime
    {
        get
        {
            if(IsActive)
            {
                return dueTime - firstInterval - executeIterations * interval; 
            }
            return 0;
        }
    }
    #endregion

#if (DEBUG && UNITY_EDITOR)
    private string m_CallingMethod = "";
#endif

    public TimerHandle(float delay, int iterations, float interval, Callback callback, ArgCallback argCallback, object arguments, int id)
    {
        this.startTime = Time.time;
        this.dueTime = Time.time + delay;
        this.firstInterval = delay;
        this.totalIterations = iterations;
        this.executeIterations = 0;
        this.interval = interval;
        this.Function = callback;
        this.ArgFunc = argCallback;
        this.arguments = arguments;
        this.isActive = true;
        this.paused = false;
        this.id = id;
    }
    /// <summary>
    /// 此方法由计时管理器调用，不可主动调用
    /// </summary>
    /// <param name="delay"></param>
    /// <param name="iterations"></param>
    /// <param name="interval"></param>
    /// <param name="callback"></param>
    /// <param name="argCallback"></param>
    /// <param name="arguments"></param>
    /// <param name="id"></param>
    public void ResetTimer(float delay, int iterations, float interval, Callback callback, ArgCallback argCallback, object arguments, int id)
    {
        this.startTime = Time.time;
        this.dueTime = Time.time + delay;
        this.firstInterval = delay;
        this.totalIterations = iterations;
        this.executeIterations = 0;
        this.interval = interval;
        this.Function = callback;
        this.ArgFunc = argCallback;
        this.arguments = arguments;
        this.isActive = true;
        this.paused = false;
        this.id = id;
        executeIterations = 0;
    }

    public void Execute()
    {
        if (Id == 0)
        {
            Cancel();
            return;
        }

        if (Function != null)
            Function();
        else if (ArgFunc != null)
            ArgFunc(arguments);
        else
        {
            Cancel();
            return;
        }

        ++executeIterations;
        if (totalIterations > 0)
        {
            if (totalIterations == executeIterations)
            {
                Cancel();
                return;
            }
        }

        currentExecuteTime = (isEgnoreTimeScale ? Time.unscaledTime : Time.time);
        dueTime += interval;

    }
    public void DelayTime()
    {
        if (paused)
        {
            if (isEgnoreTimeScale)
            {
                dueTime += Time.unscaledDeltaTime;
            }
            else
            {
                dueTime += Time.deltaTime;
            }
        }
    }

    public void Cancel()
    {
        if (isActive)
        {
            startTime = 0;
            dueTime = 0;
            interval = 0;
            isActive = false;
            paused = true;
            id = 0;
            executeIterations = 0;
            currentExecuteTime = 0;
            firstInterval = 0;
            isEgnoreTimeScale = false;
            this.Function = null;
            this.ArgFunc = null;
            this.arguments = null;
            EasyTimer.PushPool(this);
#if (UNITY_EDITOR)
            EasyTimer.EditorRefresh();
#endif
        }
    }

    public void DestroyTimerHandle()
    {
        EasyTimer.Destroy(this);
    }

    /// <summary>
    /// 计时器是否忽略时间缩放(仅在初始化计时器时有效）
    /// </summary>
    /// <param name="b"></param>
    public TimerHandle EgnoreTimeScale(bool b = false)
    {
        if (LifeTime == 0 && isActive)
        {
            if (b)
            {
                this.startTime = Time.unscaledTime;
                this.dueTime = Time.unscaledTime + firstInterval;
            }
            isEgnoreTimeScale = b;
        }
        return this;
    }

#if (UNITY_EDITOR)
    public void StoreCallingMethod()
    {
        StackTrace stackTrace = new StackTrace();

        string result = "";
        string declaringType = "";
        for (int v = 3; v < stackTrace.FrameCount; v++)
        {
            StackFrame stackFrame = stackTrace.GetFrame(v);
            declaringType = stackFrame.GetMethod().DeclaringType.ToString();
            result += " <- " + declaringType + ":" + stackFrame.GetMethod().Name.ToString();
        }

        m_CallingMethod = result;

    }
#endif
    

    public string MethodName
    {
        get
        {
            if (Function != null)
            {
                if (Function.Method != null)
                {
                    if (Function.Method.Name[0] == '<')
                        return "delegate";
                    else return Function.Method.Name;
                }
            }
            else if (ArgFunc != null)
            {
                if (ArgFunc.Method != null)
                {
                    if (ArgFunc.Method.Name[0] == '<')
                        return "delegate";
                    else return ArgFunc.Method.Name;
                }
            }
            return null;
        }
    }



    public string MethodInfo
    {
        get
        {
            string s = MethodName;
            if (!string.IsNullOrEmpty(s))
            {
                s += "(";
                if (arguments != null)
                {
                    if (arguments.GetType().IsArray)
                    {
                        object[] array = (object[])arguments;
                        foreach (object o in array)
                        {
                            s += o.ToString();
                            if (Array.IndexOf(array, o) < array.Length - 1)
                                s += ", ";
                        }
                    }
                    else
                        s += arguments;
                }
                s += ")";
            }
            else
                s = "(function = null)";

#if (DEBUG && UNITY_EDITOR)
            s += m_CallingMethod;
#endif
            return s;
        }
    }



  

   
}

