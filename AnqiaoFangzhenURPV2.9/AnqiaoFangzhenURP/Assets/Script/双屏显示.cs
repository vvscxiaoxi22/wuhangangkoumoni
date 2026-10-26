using UnityEngine;

public class 双屏显示 : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Display.displays.Length >= 2)
        {
            Display.displays[1].Activate();
            // Windows可自定义副屏分辨率，格式：宽，高，刷新率
            // Display.displays[1].Activate(1920,1080,60);
        }
    }

    // Update is called once per frame
 
}
