using System;
using UnityEngine;

public class 小车控制 : MonoBehaviour
{
    // 小车Z位置
    public float trolleyPosZ;
    // 伺服给定指令 -3000 ~ 3000
    private float motorCmd;
    // 小车物理速度 m/s，保留2位小数用于UI展示
    public float trolleySpeed;

    public float MaxSpeed = 4.0f;
    const int MotorMaxVal = 3000;
    // 位置硬限位
    const float PosMaxZ = 0f;
    const float PosMinZ = -128f;

    /// <summary>
    /// 设置伺服电机指令
    /// </summary>
    /// <param name="cmd">伺服给定值 -3000 ~ 3000</param>
    public void 移动设置(float cmd)
    {
        // 限幅，防止超出伺服范围
        motorCmd = Mathf.Clamp(cmd, -MotorMaxVal, MotorMaxVal);

        float mag = Mathf.Abs(motorCmd) / (float)MotorMaxVal;
        float speedMagnitude = mag * MaxSpeed;

        if (motorCmd < 0)
        {
            trolleySpeed = speedMagnitude;    // 负指令 → Z轴正方向
        }
        else if (motorCmd > 0)
        {
            trolleySpeed = -speedMagnitude;   // 正指令 → Z轴负方向
        }
        else
        {
            trolleySpeed = 0f;
        }

        // 四舍五入保留两位小数（UI显示用）
        trolleySpeed = (float)Math.Round(trolleySpeed, 2);
    }

    /// <summary>
    /// 更新小车位置，在Update调用
    /// </summary>
    public void UpdatePosition()
    {
        float nextPos = trolleyPosZ + trolleySpeed * Time.deltaTime;

        // 硬限位判定
        if (nextPos > PosMaxZ)
        {
            trolleyPosZ = PosMaxZ;
            trolleySpeed = 0; //触上限，停止
        }
        else if (nextPos < PosMinZ)
        {
            trolleyPosZ = PosMinZ;
            trolleySpeed = 0; //触下限，停止
        }
        else
        {
            trolleyPosZ = nextPos;
        }
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, trolleyPosZ);
        岸桥StateData.Get._岸桥State.小车位置 = -(float)Math.Round(trolleyPosZ, 2);
        岸桥StateData.Get._岸桥State.小车速度 = Math.Abs((float)Math.Round(trolleySpeed, 2));
    }

    void Update()
    {
        UpdatePosition();
    }
}
