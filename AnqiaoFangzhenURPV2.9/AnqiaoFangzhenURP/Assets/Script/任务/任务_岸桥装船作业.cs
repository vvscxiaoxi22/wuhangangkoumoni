using System;
using System.Collections.Generic;
using UnityEngine;

public class 任务_岸桥装船作业 : 任务Base
{
    public List<GameObject> 集装箱;
    private string 集卡车道;
    private string 目标箱位;
    public int 类型;//0:20尺  1:40尺  2:20尺双箱
    public List<Transform> 目标箱位坐标;
    public override void 初始化()
    {
        base.初始化();
        集卡车道 = 岸桥StateData.Get._任务State.字段1内容;
        目标箱位 = 岸桥StateData.Get._任务State.字段2内容;
        目标箱位坐标 = new List<Transform>();
        switch (岸桥StateData.Get._任务State.字段3内容)
        {
            case "20尺":
                类型 = 0;
                目标箱位坐标.Add(船贝管理.Get.箱位获取(目标箱位));
                break;
            case "40尺":
                类型 = 1;
                目标箱位坐标.Add(船贝管理.Get.箱位获取(目标箱位));
                break;
            case "20尺双箱":
                类型 = 2;
                string[] parts = 目标箱位.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var slotA = parts[0].Trim();
                var slotB = parts[1].Trim();
                目标箱位坐标.Add(船贝管理.Get.箱位获取(slotA));
                目标箱位坐标.Add(船贝管理.Get.箱位获取(slotB));
                break;
            default:
                break;
        }
        集装箱 = 集卡管理.Get.集装箱获取(集卡车道, 类型);
    }
    private void Update()
    {
        if (集装箱 != null && 目标箱位坐标 != null)
        {
            switch (类型)
            {
                case 0://20尺
                    if (集装箱.Count == 1 && 目标箱位坐标.Count == 1)
                    {
                        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 0)
                        {
                            if (Vector3.Distance(集装箱[0].transform.position, 目标箱位坐标[0].position) <= 岸桥StateData.Get._控制字段.集装箱位置距离判定)
                            {
                                base.任务完成();
                            }
                        }
                    }
                    break;
                case 1://40尺
                    if (集装箱.Count == 1 && 目标箱位坐标.Count == 1)
                    {
                        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 0)
                        {
                            if (Vector3.Distance(集装箱[0].transform.position, 目标箱位坐标[0].position) <= 岸桥StateData.Get._控制字段.集装箱位置距离判定)
                            {
                                base.任务完成();
                            }
                        }
                    }
                    break;
                case 2://20尺双箱
                    if (集装箱.Count == 2 && 目标箱位坐标.Count == 2)
                    {
                        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 0)
                        {
                            if (Vector3.Distance(集装箱[0].transform.position, 目标箱位坐标[0].position) <= 岸桥StateData.Get._控制字段.集装箱位置距离判定&& Vector3.Distance(集装箱[1].transform.position, 目标箱位坐标[1].position) <= 岸桥StateData.Get._控制字段.集装箱位置距离判定)
                            {
                                base.任务完成();
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
