using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class BulletScript : PoolAble
{
    public float destroyTime;
    // Start is called before the first frame update
    void OnEnable()
    {
        StartCoroutine(DestroyTime(destroyTime));
    }
    IEnumerator DestroyTime(float time) {
        yield return new WaitForSeconds(time);
        Pool.Release(this.gameObject);
        //Destroy(gameObject);
    }
    void OnTriggerEnter(Collider other) {
    Debug.Log("부딪힌 대상: " + other.gameObject.name);
    // 만약 여기서 "Player"가 찍힌다면 본인 몸에 맞아서 사라지는 겁니다.
}
}