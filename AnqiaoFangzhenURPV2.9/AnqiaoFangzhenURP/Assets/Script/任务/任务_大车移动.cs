using UnityEngine;

public class 任务_大车移动 : 任务Base
{
    private int 目标位置;
    public override void 初始化()
    {
        base.初始化();
        目标位置 = int.Parse(岸桥StateData.Get._任务State.字段1内容);
    }
    private void Update()
    {
        if (目标位置 < 0)
        {
            if (岸桥StateData.Get._岸桥State.大车位置 <= 目标位置)
            {
                base.任务完成();
            }
        }
        else if (目标位置 > 0)
        {
            if (岸桥StateData.Get._岸桥State.大车位置 >= 目标位置)
            {
                base.任务完成();
            }
        }
    }
}
