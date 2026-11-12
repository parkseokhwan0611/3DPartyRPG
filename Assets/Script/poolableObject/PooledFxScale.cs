using UnityEngine;

// 풀 이펙트를 잠깐 키워서 쓰고, 풀에 반납될 때(비활성화) 원래 크기로 되돌린다.
// 같은 풀 키를 크기 1로 쓰는 곳이 있어도 키운 크기가 남지 않게 하기 위함 (큰 소환수의 소환·사라짐 이펙트 등)
public class PooledFxScale : MonoBehaviour
{
    private Vector3 _baseScale;

    public static void Apply(GameObject go, float scale)
    {
        if (go == null) return;
        if (!go.TryGetComponent(out PooledFxScale s))
        {
            s = go.AddComponent<PooledFxScale>();
            s._baseScale = go.transform.localScale; // 처음 붙일 때의 크기가 원본 (반납 때마다 되돌리므로 이후에도 유지)
        }
        go.transform.localScale = s._baseScale * scale;
    }

    void OnDisable()
    {
        transform.localScale = _baseScale;
    }
}
