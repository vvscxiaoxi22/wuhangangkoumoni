using UnityEngine;

public class 声音管理 : MonoBehaviour
{
    #region 单例
    private static 声音管理 _Instance;
    public static 声音管理 Get
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindFirstObjectByType(typeof(声音管理)) as 声音管理;
            }
            return _Instance;
        }
    }
    #endregion
    public AudioSource audioSource;
    public AudioClip[] audioClip;
    public void 大车鸣笛()
    {
        audioSource.PlayOneShot(audioClip[0]);
    }
}
