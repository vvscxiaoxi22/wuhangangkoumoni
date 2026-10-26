using System.Collections.Generic;
using UnityEngine;

public class 集卡管理 : MonoBehaviour
{
    #region 单例
    private static 集卡管理 _Instance;
    public static 集卡管理 Get
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindFirstObjectByType(typeof(集卡管理)) as 集卡管理;
            }
            return _Instance;
        }
    }
    #endregion
    public List<GameObject> 集装箱获取(string 车道, int 类型, bool 开关=true)//0:20英尺  1:40英尺  2:双20
    {
        var 集卡 = transform.Find(车道);
        集卡.gameObject.SetActive(true);
        List<GameObject> list = new List<GameObject>();
        switch (类型)
        {
            case 0://20
                var 箱1 = 集卡.Find("集装箱").Find("20_1").gameObject;
                箱1.SetActive(开关);
                list.Add(箱1);
                break;
            case 1://40
                var 箱2 = 集卡.Find("集装箱").Find("40").gameObject;
                箱2.SetActive(开关);
                list.Add(箱2);
                break;
            case 2://双20
                var 箱3 = 集卡.Find("集装箱").Find("20_1").gameObject;
                var 箱4 = 集卡.Find("集装箱").Find("20_2").gameObject;
                箱3.SetActive(开关);
                箱4.SetActive(开关);
                list.Add(箱3);
                list.Add(箱4);
                break;
            default:
                break;
        }
        return list;
    }
}
