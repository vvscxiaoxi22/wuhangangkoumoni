using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
public class 吊具控制 : MonoBehaviour
{
    [Header("着箱状态")]
    int i = 0;
    [Header("吊具刚体")]
    public Rigidbody hookRb;
    [Header("升降控制")]
    public float hoistSpeed = 1.5f;
    public float maxY = 55f;            //上限位 Y最大值
    public float minY = 3.8f;             //下限位 Y最小值
    const int MotorMaxVal = 3000;
    [Header("Y 轴均衡弹簧参数")]
    public float yStiffness = 22f;
    public float yDamping = 3f;
    public float maxYForce = 12f;  // Y 方向最大出力，控制均衡力度
    private float targetY;
    public List<GameObject> 着箱集装箱 = new List<GameObject>();
    private FixedJoint boxJoint;
    public bool isSpreaderMoving = false;//尺寸切换状态
    private int currentSpreaderSize = 1;//当前尺寸
    public LayerMask groundLayer;  // 地面/甲板层

    public Transform 左伸缩, 右伸缩, 中伸缩;
    public Transform 位置20, 位置40;

    public Vector3 Center40, Size40;
    public Vector3 Center20, Size20;
    public Vector3 CenterNormal, SizeNormal;

    public Transform 中间吊具;
    private Tween _单双切换tween, _陆左导卫tween, _海左导卫tween, _陆右导卫tween, _海右导卫tween;

    public Transform 陆左导卫, 海左导卫, 陆右导卫, 海右导卫;
    private int 陆左角度 = 1, 海左角度 = 1, 陆右角度 = 1, 海右角度 = 1;


    private float 旋转速度;
    [Header("最大角度限制 ±5°")]
    private float maxAngle = 5f;

    private float 左右倾斜速度;
    private float 前后倾斜速度;
    [Header("最大角度限制 ±3°")]
    private float maxAngle2 = 3f;

    // 当前相对于初始角度的偏移量（重点！用来做角度限制）
    private float currentAngleOffset;
    private float targetAngleOffset;
    public void 全部导卫控制(float type)
    {
        if (type > 1500)//全下
        {
            岸桥StateData.Get._岸桥State.导板全部上下 = 2;
            if (岸桥StateData.Get._岸桥State.陆左导板上下 == 0)
            {
                陆左导卫控制(1);
            }
            if (岸桥StateData.Get._岸桥State.海左导板上下 == 0)
            {
                海左导卫控制(1);
            }
            if (岸桥StateData.Get._岸桥State.陆右导板上下 == 0)
            {
                陆右导卫控制(1);
            }
            if (岸桥StateData.Get._岸桥State.海右导板上下 == 0)
            {
                海右导卫控制(1);
            }
            EasyTimer.CreateTimer(1, () =>
            {
                岸桥StateData.Get._岸桥State.导板全部上下 = 1;
            });
        }
        else if (type < -1500)//全上
        {
            岸桥StateData.Get._岸桥State.导板全部上下 = 2;
            if (岸桥StateData.Get._岸桥State.陆左导板上下 == 1)
            {
                陆左导卫控制(0);
            }
            if (岸桥StateData.Get._岸桥State.海左导板上下 == 1)
            {
                海左导卫控制(0);
            }
            if (岸桥StateData.Get._岸桥State.陆右导板上下 == 1)
            {
                陆右导卫控制(0);
            }
            if (岸桥StateData.Get._岸桥State.海右导板上下 == 1)
            {
                海右导卫控制(0);
            }
            EasyTimer.CreateTimer(1, () =>
            {
                岸桥StateData.Get._岸桥State.导板全部上下 = 0;
            });
        }
    }
    public void 陆左导卫控制(int type)
    {

        if (type == 0)//上
        {
            if (陆左角度 == 1)
            {
                return;
            }
            _陆左导卫tween = DOTween.To(() => 179, x =>
            {
                岸桥StateData.Get._岸桥State.陆左导板上下 = 2;
                // 只更新X，Y、Z沿用原始值，完全不动
                陆左导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 1, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {

                岸桥StateData.Get._岸桥State.陆左导板上下 = 0;
                陆左角度 = 1;
            }
            );
        }
        else if (type == 1)//下
        {
            if (陆左角度 == 179)
            {
                return;
            }
            _陆左导卫tween = DOTween.To(() => 1, x =>
            {
                岸桥StateData.Get._岸桥State.陆左导板上下 = 2;

                // 只更新X，Y、Z沿用原始值，完全不动
                陆左导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 179, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {
                岸桥StateData.Get._岸桥State.陆左导板上下 = 1;
                陆左角度 = 179;
            });
        }
    }
    public void 海左导卫控制(int type)
    {
        if (type == 0)//上
        {
            if (海左角度 == 1)
            {
                return;
            }
            _海左导卫tween = DOTween.To(() => 179, x =>
            {
                岸桥StateData.Get._岸桥State.海左导板上下 = 2;
                // 只更新X，Y、Z沿用原始值，完全不动
                海左导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 1, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {
                岸桥StateData.Get._岸桥State.海左导板上下 = 0;
                海左角度 = 1;
            }
            );
        }
        else if (type == 1)//下
        {
            if (海左角度 == 179)
            {
                return;
            }
            _海左导卫tween = DOTween.To(() => 1, x =>
            {
                岸桥StateData.Get._岸桥State.海左导板上下 = 2;
                // 只更新X，Y、Z沿用原始值，完全不动
                海左导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 179, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {
                岸桥StateData.Get._岸桥State.海左导板上下 = 1;
                海左角度 = 179;
            });
        }
    }
    public void 陆右导卫控制(int type)
    {
        if (type == 0)//上
        {
            if (陆右角度 == 1)
            {
                return;
            }
            _陆右导卫tween = DOTween.To(() => 179, x =>
            {
                岸桥StateData.Get._岸桥State.陆右导板上下 = 2;
                // 只更新X，Y、Z沿用原始值，完全不动
                陆右导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 1, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {
                岸桥StateData.Get._岸桥State.陆右导板上下 = 0;
                陆右角度 = 1;
            }
            );
        }
        else if (type == 1)//下
        {
            if (陆右角度 == 179)
            {
                return;
            }
            _陆右导卫tween = DOTween.To(() => 1, x =>
            {
                岸桥StateData.Get._岸桥State.陆右导板上下 = 2;
                // 只更新X，Y、Z沿用原始值，完全不动
                陆右导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 179, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {
                岸桥StateData.Get._岸桥State.陆右导板上下 = 1;
                陆右角度 = 179;
            });
        }
    }
    public void 海右导卫控制(int type)
    {
        if (type == 0)//上
        {
            if (海右角度 == 1)
            {
                return;
            }
            _海右导卫tween = DOTween.To(() => 179, x =>
            {
                岸桥StateData.Get._岸桥State.海右导板上下 = 2;
                // 只更新X，Y、Z沿用原始值，完全不动
                海右导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 1, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {
                岸桥StateData.Get._岸桥State.海右导板上下 = 0;
                海右角度 = 1;
            }
            );
        }
        else if (type == 1)//下
        {
            if (海右角度 == 179)
            {
                return;
            }
            _海右导卫tween = DOTween.To(() => 1, x =>
            {
                岸桥StateData.Get._岸桥State.海右导板上下 = 2;
                // 只更新X，Y、Z沿用原始值，完全不动
                海右导卫.localEulerAngles = new Vector3(x, 0, 0);
            }, 179, 1.0f).SetEase(Ease.Linear).OnComplete(() =>
            {
                岸桥StateData.Get._岸桥State.海右导板上下 = 1;
                海右角度 = 179;
            });
        }
    }
    private void OnDestroy()
    {
        _单双切换tween?.Kill();
        _陆左导卫tween?.Kill();
        _海左导卫tween?.Kill();
        _陆右导卫tween?.Kill();
        _海右导卫tween?.Kill();
    }
    public void 切换单双模式(int type)//0:单箱,1:双箱
    {
        if (type == 0)//单箱模式
        {
            中伸缩.gameObject.SetActive(false);
            岸桥StateData.Get._岸桥State.单双箱选择 = 0;
            _单双切换tween = 中间吊具.DOLocalMove(Vector3.zero, 1)
            .SetEase(Ease.Linear);
        }
        else//双箱模式
        {
            中伸缩.gameObject.SetActive(true);
            岸桥StateData.Get._岸桥State.单双箱选择 = 1;
            _单双切换tween = 中间吊具.DOLocalMove(new Vector3(0, -0.145f, 0), 1)
            .SetEase(Ease.Linear);
        }
    }

    public void 切换碰撞尺寸(int type)//0:20,1:40,2:正常
    {
        if (type == 0)
        {
            GetComponent<BoxCollider>().center = Center20;
            GetComponent<BoxCollider>().size = Size20;
        }
        else if (type == 1)
        {
            GetComponent<BoxCollider>().center = Center40;
            GetComponent<BoxCollider>().size = Size40;
        }
        else
        {
            GetComponent<BoxCollider>().center = CenterNormal;
            GetComponent<BoxCollider>().size = SizeNormal;
        }
    }
    public void 添加约束(GameObject 目标)
    {

        Transform containerTf = 目标.transform;
        Transform spreaderTf = hookRb.transform;
        ParentConstraint parentConstraint = 目标.AddComponent<ParentConstraint>();
        ConstraintSource source = new ConstraintSource();
        source.sourceTransform = transform;
        source.weight = 1f;
        parentConstraint.AddSource(source);
        // ========== 核心：捕获当前相对偏移，不会跳位置 ==========
        parentConstraint.SetTranslationOffset(0, containerTf.position - spreaderTf.position);
        Quaternion relativeRot = Quaternion.Inverse(spreaderTf.rotation) * containerTf.rotation;
        parentConstraint.SetRotationOffset(0, relativeRot.eulerAngles);
        parentConstraint.constraintActive = true;
        // 是否冻结某轴向（false=跟随源物体该轴位置）
    }
    public void 移除约束(GameObject 目标)
    {
        着箱集装箱.ForEach((container) =>
        {
            if (container != null)
            {
                ParentConstraint parentConstraint = container.GetComponent<ParentConstraint>();
                if (parentConstraint != null)
                {
                    Destroy(parentConstraint);
                }
            }
        });
    }
    public void 设置着箱状态(bool b, GameObject 集装箱)
    {

        if (岸桥StateData.Get._岸桥State.吊具尺寸 == 0)//20
        {
            if (b)
            {
                i += 1;
            }
            else
            {
                i -= 1;
            }
            if (i >= 4)// 着箱状态
            {
                岸桥StateData.Get._岸桥State.着箱 = true;
                着箱集装箱.Add(集装箱);
            }
            else
            {
                岸桥StateData.Get._岸桥State.着箱 = false;
                着箱集装箱.Clear();
            }
        }
        else
        {

            if (岸桥StateData.Get._岸桥State.单双箱选择 == 0)//40单箱
            {
                if (集装箱.GetComponent<集装箱State>().类型 == 1)
                {
                    if (b)
                    {
                        i += 1;
                    }
                    else
                    {
                        i -= 1;
                    }
                }

                if (i >= 4)// 着箱状态
                {
                    岸桥StateData.Get._岸桥State.着箱 = true;
                    着箱集装箱.Add(集装箱);
                }
                else
                {
                    岸桥StateData.Get._岸桥State.着箱 = false;
                    着箱集装箱.Clear();
                }
            }
            else//20双箱
            {
                if (集装箱.GetComponent<集装箱State>().类型 == 0)
                {
                    if (b)
                    {
                        i += 1;
                    }
                    else
                    {
                        i -= 1;
                    }
                }

                if (i >= 4)// 着箱状态
                {
                    岸桥StateData.Get._岸桥State.着箱 = true;
                    if (!着箱集装箱.Contains(集装箱))
                    {
                        着箱集装箱.Add(集装箱);
                    }
                }
                else
                {
                    岸桥StateData.Get._岸桥State.着箱 = false;
                    着箱集装箱.Clear();
                }
            }
        }

    }
    public void 设置抓取(int i)
    {
        if (i == 0)
        {
            开锁解绑();
        }
        else if (i == 1)
        {
            闭锁抓取();
        }
    }
    public void 闭锁抓取()
    {
        if (着箱集装箱 != null)
        {
            着箱集装箱.ForEach((container) =>
            {
                if (container != null)
                {
                    container.GetComponent<BoxCollider>().enabled = false;
                    if (岸桥StateData.Get._岸桥State.吊具尺寸 == 0)//20尺寸
                    {
                        切换碰撞尺寸(0);
                    }
                    else//40尺寸
                    {
                        切换碰撞尺寸(1);
                    }

                    添加约束(container);
                    Debug.Log("✅ 集装箱已物理绑定，可以起升");
                }
            });
        }
    }
    public void 开锁解绑()
    {
        切换碰撞尺寸(2);
        着箱集装箱.ForEach((container) =>
        {
            if (container != null)
            {
                container.GetComponent<BoxCollider>().enabled = true;
                移除约束(container);
                Debug.Log("🔓 解除集装箱绑定");
            }
        });
    }
    public void 设置升降(float f)
    {
        float motorCmd = Mathf.Clamp(f, -MotorMaxVal, MotorMaxVal);

        // 根据满载/空载选择最大速度
        float speed = Mathf.Abs(motorCmd) / (float)MotorMaxVal;
        float mag = speed * 50;
        if (motorCmd < 0)
        {
            hoistSpeed = -mag;    //负指令 → Y+ 向上
        }
        else if (motorCmd > 0)
        {
            hoistSpeed = mag;   //正指令 → Y- 向下
        }
        else
        {
            hoistSpeed = 0f;
        }

        岸桥StateData.Get._岸桥State.吊具速度 = Math.Abs((float)Math.Round(speed * 3, 2));
    }
    public void 设置旋转(float cmd)
    {

        var motorCmd = Mathf.Clamp(cmd, -3000, 3000);

        float mag = Mathf.Abs(motorCmd) / (float)MotorMaxVal;
        float speedMagnitude = mag * 1;

        if (motorCmd < 0)
        {
            旋转速度 = -speedMagnitude;
        }
        else if (motorCmd > 0)
        {
            旋转速度 = speedMagnitude;
        }
        else
        {
            旋转速度 = 0;
        }
    }
    public void 设置左右倾斜(float cmd)
    {
        var motorCmd = Mathf.Clamp(cmd, -3000, 3000);

        float mag = Mathf.Abs(motorCmd) / (float)MotorMaxVal;
        float speedMagnitude = mag * 1;

        if (motorCmd < 0)
        {
            左右倾斜速度 = -speedMagnitude;
        }
        else if (motorCmd > 0)
        {
            左右倾斜速度 = speedMagnitude;
        }
        else
        {
            左右倾斜速度 = 0;
        }
    }
    public void 设置前后倾斜(float cmd)
    {
        var motorCmd = Mathf.Clamp(cmd, -3000, 3000);

        float mag = Mathf.Abs(motorCmd) / (float)MotorMaxVal;
        float speedMagnitude = mag * 1;

        if (motorCmd < 0)
        {
            前后倾斜速度 = speedMagnitude;
        }
        else if (motorCmd > 0)
        {
            前后倾斜速度 = -speedMagnitude;
        }
        else
        {
            前后倾斜速度 = 0;
        }
    }
    public void 吊具回零()
    {
        旋转速度 = 0;
        左右倾斜速度 = 0;
        前后倾斜速度 = 0;
        岸桥StateData.Get._岸桥State.吊具回零状态 = true;
        transform.DOLocalRotate(Vector3.zero, 2).OnComplete(() =>
        {
            岸桥StateData.Get._岸桥State.吊具回零状态 = false;
        });
    }
    public void 设置20尺寸()
    {
        切换尺寸(0);
    }
    public void 设置40尺寸()
    {
        切换尺寸(1);
    }
    private void 切换尺寸(int i)//0:20,1:40
    {
        // ========== 安全联锁判断 ==========
        if (isSpreaderMoving)//伸缩正在运行，禁止重复操作
        {
            return;
        }
        if (岸桥StateData.Get._岸桥State.着箱 == true)//已着箱，不能伸缩
        {
            return;
        }

        if (岸桥StateData.Get._岸桥State.集装箱状态 != 0)//联锁禁止：吊具已闭锁带箱，不能伸缩
        {
            return;
        }

        if (currentSpreaderSize == i)//当前已经是目标尺寸，无需切换
        {
            return;
        }
        // 全部条件满足，启动伸缩协程
        StartCoroutine(SpreaderSwitchCoroutine(i));
    }
    private IEnumerator SpreaderSwitchCoroutine(int i)
    {
        isSpreaderMoving = true;
        切换单双模式(0);
        // 延时模拟伸缩运行
        float timer = 0;
        while (timer < 2)
        {
            timer += Time.deltaTime;
            float t = timer / 2;
            // 平滑插值（lerp）
            if (i == 0)//20
            {
                左伸缩.localPosition = new Vector3(Mathf.Lerp(位置40.localPosition.x, 位置20.localPosition.x, t), 左伸缩.localPosition.y, 左伸缩.localPosition.z);
                右伸缩.localPosition = new Vector3(Mathf.Lerp(-位置40.localPosition.x, -位置20.localPosition.x, t), 左伸缩.localPosition.y, 左伸缩.localPosition.z);
            }
            else//40
            {
                左伸缩.localPosition = new Vector3(Mathf.Lerp(位置20.localPosition.x, 位置40.localPosition.x, t), 左伸缩.localPosition.y, 左伸缩.localPosition.z);
                右伸缩.localPosition = new Vector3(Mathf.Lerp(-位置20.localPosition.x, -位置40.localPosition.x, t), 左伸缩.localPosition.y, 左伸缩.localPosition.z);
            }
            yield return null;
        }
        // 伸缩到位，更新状态
        if (i == 0)//20
        {
            岸桥StateData.Get._岸桥State.吊具尺寸 = 0;
            左伸缩.localPosition = new Vector3(位置20.localPosition.x, 左伸缩.localPosition.y, 左伸缩.localPosition.z);
            右伸缩.localPosition = new Vector3(-位置20.localPosition.x, 左伸缩.localPosition.y, 左伸缩.localPosition.z);

        }
        else//40
        {
            岸桥StateData.Get._岸桥State.吊具尺寸 = 1;
            左伸缩.localPosition = new Vector3(位置40.localPosition.x, 左伸缩.localPosition.y, 左伸缩.localPosition.z);
            右伸缩.localPosition = new Vector3(-位置40.localPosition.x, 左伸缩.localPosition.y, 左伸缩.localPosition.z);
        }
        currentSpreaderSize = i;
        isSpreaderMoving = false;
        Debug.Log($"✅ 吊具伸缩完成，当前吊具尺寸：{currentSpreaderSize}");
    }
    float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
    void Update()
    {
        #region 吊具升降
        targetY = hookRb.position.y;
        // W/S 修改目标高度
        //float input = Input.GetAxis ("Vertical");
        targetY += 1 * hoistSpeed * Time.deltaTime;
        if (hoistSpeed == 0)
        {
            Vector3 vel = hookRb.linearVelocity;
            vel.y = 0;
            hookRb.linearVelocity = vel;
            hookRb.angularVelocity = vel;

        }
        岸桥StateData.Get._岸桥State.吊具高度 = (float)Math.Round(transform.localPosition.y, 2);
        #endregion
        #region 吊具水平旋转

        transform.Rotate(前后倾斜速度 * 1.5f * Time.deltaTime, 旋转速度 * 3 * Time.deltaTime, 左右倾斜速度 * 1.5f * Time.deltaTime);
        var Normalx = Mathf.Clamp(NormalizeAngle(transform.localEulerAngles.x), -maxAngle2, maxAngle2);
        var Normaly = Mathf.Clamp(NormalizeAngle(transform.localEulerAngles.y), -maxAngle, maxAngle);
        var Normalz = Mathf.Clamp(NormalizeAngle(transform.localEulerAngles.z), -maxAngle2, maxAngle2);
        transform.localEulerAngles = new Vector3(Normalx, Normaly, Normalz);
        岸桥StateData.Get._岸桥State.吊具水平回旋角度 = (float)Math.Round(Normaly, 2);
        岸桥StateData.Get._岸桥State.吊具前后倾斜角度 = -(float)Math.Round(Normalx, 2);
        岸桥StateData.Get._岸桥State.吊具左右倾斜角度 = (float)Math.Round(Normalz, 2);
        #endregion
    }

    void FixedUpdate()
    {
        float errorY = targetY - hookRb.position.y;
        // 基础弹簧力
        float springForceY = errorY * yStiffness;
        // 均衡限制：限制 Y 方向最大力，不会误差大就瞬间爆力
        springForceY = Mathf.Clamp(springForceY, -maxYForce, maxYForce);
        // Y 方向阻尼，抵消 Y 向速度
        float dampForceY = -hookRb.linearVelocity.y * yDamping;
        // 合力，只施加 Y 方向
        float totalYForce = springForceY + dampForceY;
        hookRb.AddForce(0, totalYForce, 0, ForceMode.Acceleration);

        //集装箱落地判断
        if (岸桥StateData.Get._岸桥State.吊具尺寸 == 0)//20
        {
            if (着箱集装箱.Count > 0)
            {
                bool hitGround = Physics.Raycast(
                着箱集装箱[0].transform.Find("地面检测").position,
                Vector3.down,
                out RaycastHit hit,
                0.2f,
                groundLayer
                 );
                if (hitGround == true)
                {
                    if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 1)
                    {
                        岸桥StateData.Get._岸桥State.集装箱状态 = 2;// 2:抓取落地
                    }
                }
                else
                {
                    if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 1)
                    {
                        岸桥StateData.Get._岸桥State.集装箱状态 = 1;//1:抓取空中 
                    }
                }
            }
            else
            {
                岸桥StateData.Get._岸桥State.集装箱状态 = 0;//0:未抓取  
            }
        }
        else
        {
            if (岸桥StateData.Get._岸桥State.单双箱选择 == 0)//40单箱
            {
                if (着箱集装箱.Count > 0)
                {
                    bool hitGround = Physics.Raycast(
                    着箱集装箱[0].transform.Find("地面检测").position,
                    Vector3.down,
                    out RaycastHit hit,
                    0.2f,
                    groundLayer
                     );
                    if (hitGround == true)
                    {
                        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 1)
                        {
                            岸桥StateData.Get._岸桥State.集装箱状态 = 2;// 2:抓取落地
                        }
                    }
                    else
                    {
                        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 1)
                        {
                            岸桥StateData.Get._岸桥State.集装箱状态 = 1;//1:抓取空中 
                        }
                    }
                }
                else
                {
                    岸桥StateData.Get._岸桥State.集装箱状态 = 0;//0:未抓取  
                }
            }
            else//40双箱
            {
                if (着箱集装箱.Count > 0)
                {
                    List<bool> checks = new List<bool>(); ;
                    着箱集装箱.ForEach((container) =>
                    {
                        bool hitGround = Physics.Raycast(
                        container.transform.Find("地面检测").position,
                        Vector3.down,
                        out RaycastHit hit,
                        0.2f,
                        groundLayer
                         );
                        checks.Add(hitGround);

                    });
                    bool allPass = checks.All(x => x);
                    if (allPass == true)
                    {
                        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 1)
                        {
                            岸桥StateData.Get._岸桥State.集装箱状态 = 2;// 2:抓取落地
                        }
                    }
                    else
                    {
                        if (岸桥StateData.Get._岸桥State.吊具开闭锁 == 1)
                        {
                            岸桥StateData.Get._岸桥State.集装箱状态 = 1;//1:抓取空中 
                        }
                    }

                }
                else
                {
                    岸桥StateData.Get._岸桥State.集装箱状态 = 0;//0:未抓取  
                }
            }

        }
    }
}