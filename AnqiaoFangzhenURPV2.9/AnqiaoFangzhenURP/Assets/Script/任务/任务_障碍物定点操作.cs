using UnityEngine;

public class 任务_障碍物定点操作 : 任务Base
{
    public Transform 集装箱, 终点;
    public override void 初始化()
    {
        base.初始化();
    }
    // Update is called once per frame
    void Update()
    {
        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 0)
        {
            if (Vector3.Distance(集装箱.position, 终点.position) <= 岸桥StateData.Get._控制字段.集装箱位置距离判定)
            {
                base.任务完成();
            }
        }
    }
}
