using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;

public class 船贝管理 : MonoBehaviour
{
    #region 单例
    private static 船贝管理 _Instance;
    public static 船贝管理 Get
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindFirstObjectByType(typeof(船贝管理)) as 船贝管理;
            }
            return _Instance;
        }
    }
    #endregion
    [Header("材质组")]
    public List<Material> materialList = new List<Material>();
    public Transform rootParent;
    [ContextMenu("随机集装箱材质")]
    public void 随机集装箱材质()
    {
        // 过滤掉null材质
        var validMats = materialList.Where(m => m != null).ToList();
        if (validMats.Count == 0)
        {
            return;
        }

        // 获取所有子物体的MeshRenderer，包括深层子物体
        MeshRenderer[] allRenderers = rootParent.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        int changedCount = 0;

        //Undo.RecordObjects(allRenderers.Select(r => r as Object).ToArray(), "集装箱随机材质赋值");

        foreach (var mr in allRenderers)
        {
            string objName = mr.gameObject.name;
            if (objName.Contains("20寸集装箱") || objName.Contains("40寸集装箱"))
            {
                // 随机取材质
                int randIndex = Random.Range(0, validMats.Count);
                Material selectedMat = validMats[randIndex];
                mr.sharedMaterial = selectedMat;
                changedCount++;
            }
        }

        Debug.Log($"【集装箱材质分配】成功修改数量：{changedCount}");
    }

    public Transform 箱位获取(string 箱号)
    {
        string sBay = 箱号.Substring(0, 2);
        string sRow = 箱号.Substring(2, 2);
        string sTier = 箱号.Substring(4, 2);
        return (transform.Find(sBay).Find(sRow).Find(sTier));
    }
}
