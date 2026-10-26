using System;
using UnityEngine;

public class 大车控制 : MonoBehaviour
{
    // 大车当前位置 X
    public float cartPosX;
    // 伺服给定指令 -3000 ~ 3000
    private float motorCmd;
    // 输出物理速度 m/s
    public float cartSpeed;

    // 最大物理速度
    public float MaxSpeed = 0.75f;
    const int MotorMaxVal = 3000;

    public void 大车鸣笛()
    {
        声音管理.Get.大车鸣笛();
        任务Manager.Get.自动关闭();
    }
    /// <summary>
    /// 设置伺服电机指令
    /// </summary>
    /// <param name="cmd"> -3000 ~ 3000 </param>
    public void 移动设置(float cmd)
    {
        // 钳位，防止超范围
        motorCmd = Mathf.Clamp(cmd, -MotorMaxVal, MotorMaxVal);

        float mag = Mathf.Abs(motorCmd) / (float)MotorMaxVal;
        float speedMagnitude = mag * MaxSpeed;

        if (motorCmd < 0)
        {
            cartSpeed = speedMagnitude; // 负数指令 → X正方向
        }
        else if (motorCmd > 0)
        {
            cartSpeed = -speedMagnitude; // 正数指令 → X负方向
        }
        else
        {
            cartSpeed = 0;
        }
    }

    /// <summary>
    /// 帧更新位置，放到Update里调用，Time.deltaTime是秒
    /// </summary>
    public void UpdatePosition()
    {
        cartPosX += cartSpeed * Time.deltaTime;
        transform.localPosition = new Vector3(cartPosX, transform.localPosition.y, transform.localPosition.z);
        //数据设置
        岸桥StateData.Get._岸桥State.大车位置 = -(float)Math.Round(cartPosX, 2);
        岸桥StateData.Get._岸桥State.大车速度 = Math.Abs((float)Math.Round(cartSpeed, 2));
    }

    void Update()
    {
        UpdatePosition();
    }

}
