using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class BulletScript : PoolAble
{
    public float destroyTime;

    private Coroutine destroyCoroutine;

    void OnEnable()
    {
        if (destroyCoroutine != null)
            StopCoroutine(destroyCoroutine);
        destroyCoroutine = StartCoroutine(DestroyTime(destroyTime));
    }

    void OnDisable()
    {
        destroyCoroutine = null;
    }

    IEnumerator DestroyTime(float time)
    {
        yield return new WaitForSeconds(time);
        destroyCoroutine = null;
        Pool.Release(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("부딪힌 대상: " + other.gameObject.name);
    }
}