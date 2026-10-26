using UnityEditor.Purchasing;
using UnityEngine;
using UnityEngine.UIElements;

public class 任务Manager : MonoBehaviour
{
    #region 单例
    private static 任务Manager _Instance;
    public static 任务Manager Get
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindFirstObjectByType(typeof(任务Manager)) as 任务Manager;
            }
            return _Instance;
        }
    }
    #endregion
    public UIDocument mainUIDoc;
    private VisualElement _mainRoot;
    void Awake()
    {
        _mainRoot = mainUIDoc.rootVisualElement;
    }
    private void Start()
    {
        岸桥StateData.Get._任务State.得分 = 100;
        岸桥StateData.Get._任务State.任务描述 = 岸桥StateData.Get._任务信息._信息[岸桥StateData.Get._任务State.任务名称].任务描述;
        int index = 0;
        foreach (var item in 岸桥StateData.Get._任务信息._信息[岸桥StateData.Get._任务State.任务名称]._目标)
        {
            if (index == 0) // 0=第一个，
            {
                岸桥StateData.Get._任务State.字段1标题 = item.Key;
                岸桥StateData.Get._任务State.字段1内容 = item.Value[岸桥StateData.Get._任务State.目标ID];
            }
            if (index == 1) //1 = 第二个
            {
                岸桥StateData.Get._任务State.字段2标题 = item.Key;
                岸桥StateData.Get._任务State.字段2内容 = item.Value[岸桥StateData.Get._任务State.目标ID];
            }
            if (index == 2) //1 = 第三个
            {
                岸桥StateData.Get._任务State.字段3标题 = item.Key;
                岸桥StateData.Get._任务State.字段3内容 = item.Value[岸桥StateData.Get._任务State.目标ID];
                break;
            }
            index++;
        }
        实训提示();
    }
    private void 实训提示()
    {
        VisualElement panelMain = _mainRoot.Q<VisualElement>("task-prompt");
        panelMain.style.display = DisplayStyle.Flex;
        Button btn1 = _mainRoot.Q<Button>("RenwuButton");
        btn1.ClearBindings();
        btn1.clicked += () =>
        {
            panelMain.style.display = DisplayStyle.None;
            开始任务();
        };
    }
    public void 自动关闭()
    {
        VisualElement panelMain = _mainRoot.Q<VisualElement>("task-prompt");
        panelMain.style.display = DisplayStyle.None;
        开始任务();
    }
    private void 开始任务()
    {
        //倒计时.....
        transform.Find(岸桥StateData.Get._任务State.任务名称).gameObject.SetActive(true);
        计时器.Get.StartTimer();
    }
    public void 完成任务()
    {
        计时器.Get.PauseTimer();
        VisualElement panelMain = _mainRoot.Q<VisualElement>("completion-prompt");
        panelMain.style.display = DisplayStyle.Flex;
    }
}
