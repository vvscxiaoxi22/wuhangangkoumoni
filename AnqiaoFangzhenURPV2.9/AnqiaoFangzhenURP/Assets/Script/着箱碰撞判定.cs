using UnityEngine;

public class 着箱碰撞判定 : MonoBehaviour
{
    public 吊具控制 _吊具控制;
    private void OnTriggerEnter(Collider other)
    {
        if (other.name == "锁孔")
        {
            _吊具控制.设置着箱状态(true, other.transform.parent.gameObject);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.name == "锁孔")
        {
            _吊具控制.设置着箱状态(false, other.transform.parent.gameObject);
        }
    }
}
