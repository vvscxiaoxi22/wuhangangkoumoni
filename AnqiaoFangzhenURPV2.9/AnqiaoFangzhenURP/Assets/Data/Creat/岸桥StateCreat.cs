using Unity.Properties;
using UnityEngine;

[CreateAssetMenu(fileName = "岸桥State", menuName = "Data/岸桥State")]
public class 岸桥StateCreat : ScriptableObject
{
    public float 大车速度 = 0;
    public float 大车位置 = 0;
    public float 小车速度 = 0;
    public float 小车位置 = 0;
    public float 吊具高度 = 0;
    public float 吊具速度 = 0;

    public float 吊具旋转速度 = 0;
    public float 吊具水平回旋角度 = 0;
    public float 吊具前后倾斜速度 = 0;
    public float 吊具前后倾斜角度 = 0;
    public float 吊具左右倾斜速度 = 0;
    public float 吊具左右倾斜角度 = 0;
    public int 海左导板上下 = 0;//上0下1运行2
    public int 海右导板上下 = 0;//上0下1运行2
    public int 陆左导板上下 = 0;//上0下1运行2
    public int 陆右导板上下 = 0;//上0下1运行2
    public float 导板全部上下 = 0;//上0下1
    public bool 吊具回零 = false;
    public bool 吊具回零状态 = false;//true是回零中
    public bool 吊具泵开关 = true;
    public int 吊具尺寸 = 1;//0:20英尺  1:40英尺 
    public bool 大车鸣笛 = false;
    public bool 控制合关 = true;//true:合  false:关
    public int 吊具开闭锁 = 0;//0:开  1:闭
    public bool 夹松轮 = false;//false:松  true:夹
    public int 单双箱选择 = 0;//0:单  1:双
    public bool 着箱 = false;
    public int 集装箱状态 = 0; //0:未抓取    1:抓取空中  2:抓取落地
    public int 俯仰角度 = 0;

    public void ResetDefault()
    {
        大车速度 = 0;
        大车位置 = 0;
        小车速度 = 0;
        小车位置 = 0;
        吊具高度 = 0;
        吊具速度 = 0;
        吊具旋转速度 = 0;
        吊具水平回旋角度 = 0;
        吊具前后倾斜速度 = 0;
        吊具前后倾斜角度 = 0;
        吊具左右倾斜速度 = 0;
        吊具左右倾斜角度 = 0;

        海左导板上下 = 0;
        海右导板上下 = 0;
        陆左导板上下 = 0;
        陆右导板上下 = 0;
        导板全部上下 = 0;

        吊具回零 = false;
        吊具回零状态 = false;
        吊具泵开关 = true;
        吊具尺寸 = 1;
        大车鸣笛 = false;
        控制合关 = true;
        吊具开闭锁 = 0;
        夹松轮 = false;
        单双箱选择 = 0;
        着箱 = false;
        集装箱状态 = 0;
        俯仰角度 = 0;
    }
}
