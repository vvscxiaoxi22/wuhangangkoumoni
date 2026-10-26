using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "试卷类型", menuName = "Data/试卷类型")]
public class 试卷类型Creat : ScriptableObject
{
    public int 试卷类型; // 0:无, 1:实训考核, 2:故障考核
    public string 实训项目;
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
    public int 实训模式; // 1:本地, 2:远程
    public List<int> 故障内容 = new List<int>();
}
