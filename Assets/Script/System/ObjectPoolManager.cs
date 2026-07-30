using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ObjectPoolManager : MonoBehaviour
{
    [System.Serializable]
    private class ObjectInfo
    {
        // 오브젝트 이름
        public string objectName;
        // 오브젝트 풀에서 관리할 오브젝트
        public GameObject perfab;
        // 몇개를 미리 생성 해놓을건지
        public int count;
    }


    public static ObjectPoolManager instance;

    // 오브젝트풀 매니저 준비 완료표시
    public bool IsReady { get; private set; }

    [SerializeField]
    private ObjectInfo[] objectInfos = null;

    // 오브젝트풀들을 관리할 딕셔너리
    private Dictionary<string, IObjectPool<GameObject>> ojbectPoolDic = new Dictionary<string, IObjectPool<GameObject>>();

    // 오브젝트풀에서 오브젝트를 새로 생성할때 사용할 딕셔너리
    private Dictionary<string, GameObject> goDic = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this.gameObject);

        Init();
    }


    private void Init()
    {
        IsReady = false;

        if (objectInfos == null)
        {
            Debug.LogWarning("[ObjectPoolManager] Object Infos가 Inspector에 설정되지 않았습니다.");
            IsReady = true;
            return;
        }

        for (int idx = 0; idx < objectInfos.Length; idx++)
        {
            string     name   = objectInfos[idx].objectName;
            GameObject prefab = objectInfos[idx].perfab;

            if (goDic.ContainsKey(name))
            {
                Debug.LogFormat("{0} 이미 등록된 오브젝트입니다.", name);
                continue;
            }

            // 풀별로 이름/프리팹을 클로저로 캡처 — 재진입(풀에서 꺼낸 오브젝트의 Awake/OnEnable이
            // 다시 GetGo()를 호출하는 경우)에도 엉뚱한 프리팹이 생성되지 않도록 공유 필드 대신 사용
            IObjectPool<GameObject> pool = null;
            pool = new ObjectPool<GameObject>(() => CreatePooledItem(prefab, pool), OnTakeFromPool, OnReturnedToPool,
                OnDestroyPoolObject, true, objectInfos[idx].count, objectInfos[idx].count);

            goDic.Add(name, prefab);
            ojbectPoolDic.Add(name, pool);

            // 미리 오브젝트 생성 해놓기
            for (int i = 0; i < objectInfos[idx].count; i++)
            {
                PoolAble poolAbleGo = CreatePooledItem(prefab, pool).GetComponent<PoolAble>();
                poolAbleGo.Pool.Release(poolAbleGo.gameObject);
            }
        }

        Debug.Log("오브젝트풀링 준비 완료");
        IsReady = true;
    }

    // 생성
    private GameObject CreatePooledItem(GameObject prefab, IObjectPool<GameObject> pool)
    {
        GameObject poolGo = Instantiate(prefab);
        poolGo.GetComponent<PoolAble>().Pool = pool;
        return poolGo;
    }

    // 대여
    private void OnTakeFromPool(GameObject poolGo)
    {
        poolGo.SetActive(true);
    }

    // 반환
    private void OnReturnedToPool(GameObject poolGo)
    {
        poolGo.SetActive(false);
    }

    // 삭제
    private void OnDestroyPoolObject(GameObject poolGo)
    {
        Destroy(poolGo);
    }

    public GameObject GetGo(string goName)
    {
        if (goDic.ContainsKey(goName) == false)
        {
            Debug.LogFormat("{0} 오브젝트풀에 등록되지 않은 오브젝트입니다.", goName);
            return null;
        }

        return ojbectPoolDic[goName].Get();
    }
}