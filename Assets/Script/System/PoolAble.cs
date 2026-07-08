using UnityEngine;
using UnityEngine.Pool;

public class PoolAble : MonoBehaviour
{
    public IObjectPool<GameObject> Pool { get; set; }

    public void ReleaseObject()
    {
        if (Pool != null)
            Pool.Release(gameObject);
        else
            Destroy(gameObject); // 풀 없이 직접 Instantiate된 경우 폴백
    }
}