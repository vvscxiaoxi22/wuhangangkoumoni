using UnityEngine;

public class TrolleyFollowHook : MonoBehaviour
{
    [Header("小车")]
    public Transform trolley;
    [Header("吊具")]
    public Transform hook;
    [Header("吊具水平跟随（平滑系数）")]
    [Tooltip("越大跟随越快，1=瞬间贴紧；0~1之间才有滞后摆动效果")]
    public float hookFollowSmooth = 4f;
    private Vector3 hookTargetPos;


    [Header("吊具刚体")]
    public Rigidbody hookRb;

    [Header("弹簧跟随参数（代替Lerp）")]
    public float springStiffness = 25f;    // 刚度：越大跟随越快（对应原来hookFollowSmooth）
    public float springDamping = 4f;

    void Start()
    {
        hookTargetPos = hook.position;
    }
    void Update()
    {

        hookTargetPos = hook.position;
        //// ===== 吊具水平目标位置：小车正下方 =====
        //hookTargetPos.x = trolley.position.x;
        //hookTargetPos.z = trolley.position.z;
        //hookTargetPos.y = hook.position.y; // 保持当前高度

        //// 平滑插值跟随，模拟钢丝绳柔性滞后
        //hook.position = Vector3.Lerp(hook.position, hookTargetPos, hookFollowSmooth * Time.deltaTime);

        // 目标水平位置锁定在小车正下方
        hookTargetPos.x = trolley.position.x;
        hookTargetPos.z = trolley.position.z;

        Vector3 error = hookTargetPos - hookRb.position;
        error.y = 0; // 关键：清除Y方向误差，Y不施加力
        // 弹簧力 + 水平阻尼（Y方向阻尼也置0）
        Vector3 springForce = error * springStiffness;
        Vector3 dampVelocity = hookRb.linearVelocity;
        dampVelocity.y = 0; // Y方向速度不做阻尼
        Vector3 dampForce = -dampVelocity * springDamping;

        Vector3 totalForce = springForce + dampForce;
        hookRb.AddForce(totalForce, ForceMode.Acceleration);

    }
    private void FixedUpdate()
    {

    }
}
