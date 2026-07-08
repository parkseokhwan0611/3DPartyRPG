using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolableObject : PoolAble
{
    public float destroyTime;

    private Coroutine destroyCoroutine;

    void OnEnable()
    {
        // 재활성화가 예상보다 빨리 일어나도 코루틴이 중첩 실행되어 이중 Pool.Release가 발생하지 않도록 가드
        if (destroyCoroutine != null)
            StopCoroutine(destroyCoroutine);
        destroyCoroutine = StartCoroutine(DestroyTime(destroyTime));
    }

    void OnDisable()
    {
        destroyCoroutine = null;
    }

    IEnumerator DestroyTime(float time) {
        yield return new WaitForSeconds(time);
        destroyCoroutine = null;
        Pool.Release(this.gameObject);
    }
}
