using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "任务State", menuName = "Data/任务State")]
public class 任务StateCreat : ScriptableObject
{
    public string 姓名;
    public string 学号;
    public string 实训项目;//岸桥集装箱起重机仿真系统 //场桥集装箱起重机仿真系统
    public string 任务名称;
    public string 任务类型;
    public int 目标ID;
    public string 任务描述;
    public string 字段1标题;
    public string 字段2标题;
    public string 字段3标题;
    public string 字段1内容;
    public string 字段2内容;
    public string 字段3内容;
    public float 实训设定时间;
    public int 实训模式;//1:本地,2:远程
    public List<int> 故障内容;

    public int 得分;
    public float 用时;
}
