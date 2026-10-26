using UnityEngine;
using Filo;

public class CraneHoistControl : MonoBehaviour
{
    public Cable[] cable;
    public float hoistSpeed = 1.5f;
    public float minLength = 1f;
    public float maxLength = 6f;

    //void Update()
    //{
    //    float input = Input.GetAxis("Vertical"); // W/S

    //    if (Mathf.Abs(input) > 0.01f)
    //    {
    //        // 取第2个Link（索引1，spool）
    //        Cable.Link spool = cable.links[1] as Cable.Link;

    //        if (spool != null)
    //        {
    //            float newLen = spool.currentLength + input * hoistSpeed * Time.deltaTime;
    //            newLen = Mathf.Clamp(newLen, minLength, maxLength);
    //            spool.currentLength = newLen;
    //        }
    //    }
    //}
}
