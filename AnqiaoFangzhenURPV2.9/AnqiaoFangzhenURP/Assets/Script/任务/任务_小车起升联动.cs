using UnityEngine;

public class 任务_小车起升联动 : 任务Base
{
    private int 小车目标位置;
    private int 吊具目标高度;
    public override void 初始化()
    {
        base.初始化();
        小车目标位置 = int.Parse(岸桥StateData.Get._任务State.字段1内容);
        吊具目标高度 = int.Parse(岸桥StateData.Get._任务State.字段2内容);
    }
    private void Update()
    {
        if (岸桥StateData.Get._岸桥State.小车位置 < 小车目标位置 + 岸桥StateData.Get._控制字段.实训移动距离判定 && 岸桥StateData.Get._岸桥State.小车位置 > 小车目标位置 - 岸桥StateData.Get._控制字段.实训移动距离判定)
        {
            if (岸桥StateData.Get._岸桥State.吊具高度 < 吊具目标高度 + 岸桥StateData.Get._控制字段.实训移动距离判定 && 岸桥StateData.Get._岸桥State.吊具高度 > 吊具目标高度 - 岸桥StateData.Get._控制字段.实训移动距离判定)
            {
                base.任务完成();
            }
        }
    }
}
