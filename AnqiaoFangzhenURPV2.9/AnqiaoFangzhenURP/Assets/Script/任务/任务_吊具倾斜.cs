using UnityEngine;

public class 任务_吊具倾斜 : 任务Base
{
    private string 目标方向;
    private int 目标角度;
    public override void 初始化()
    {
        base.初始化();
        目标方向 = 岸桥StateData.Get._任务State.字段1内容;
        目标角度 = int.Parse(岸桥StateData.Get._任务State.字段2内容);
    }
    private void Update()
    {
        switch (目标方向)
        {
            case "左倾":
                if (岸桥StateData.Get._岸桥State.吊具左右倾斜角度 <= -2)
                {
                    base.任务完成();
                }
                break;
            case "右倾":
                if (岸桥StateData.Get._岸桥State.吊具左右倾斜角度 >= 2)
                {
                    base.任务完成();
                }
                break;
            case "前倾":
                if (岸桥StateData.Get._岸桥State.吊具前后倾斜角度 <= -2)
                {
                    base.任务完成();
                }
                break;
            case "后倾":
                if (岸桥StateData.Get._岸桥State.吊具前后倾斜角度 >= 2)
                {
                    base.任务完成();
                }
                break;
            default:
                break;
        }
    }
}
