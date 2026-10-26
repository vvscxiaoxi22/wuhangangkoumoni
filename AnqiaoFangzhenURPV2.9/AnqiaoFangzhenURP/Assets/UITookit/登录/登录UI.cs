using UnityEngine;
using UnityEngine.UIElements;

public class 登录UI : MonoBehaviour
{
    public UIDocument mainUIDoc;
    private VisualElement _mainRoot;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {// 查找普通容器VisualElement（全屏面板）
        _mainRoot = mainUIDoc.rootVisualElement;
        VisualElement panelMain = _mainRoot.Q<VisualElement>("P1");
        VisualElement panelAlarm = _mainRoot.Q<VisualElement>("P2");
    
        Button btnReset = _mainRoot.Q<Button>("B1");
        Button btnReset2 = _mainRoot.Q<Button>("B2");
        btnReset.clicked += () =>
        {
            panelMain.style.display = DisplayStyle.None;
            panelAlarm.style.display = DisplayStyle.Flex;
            Debug.Log("Btn_b1 clicked");
        };
        btnReset2.clicked += () =>
        {
            panelMain.style.display = DisplayStyle.Flex;
            panelAlarm.style.display = DisplayStyle.None;
            Debug.Log("Btn_b1 clicked");
        };
    }

    // Update is called once per frame
    void Start()
    {
 
    }
}
