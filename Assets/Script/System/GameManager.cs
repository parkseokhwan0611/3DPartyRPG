using UnityEngine;

// 게임 진행 여부 플래그 — CoreSystem 프리팹에 함께 붙어 씬이 바뀌어도 유지된다.
// 예전엔 씬마다 따로 놓인 GameManager 오브젝트였고, 없으면 몬스터가 전부 멈추는 구조였음
public class GameManager : MonoBehaviour
{
    [Header("#GameControl")]
    public static GameManager instance;
    [Tooltip("끄면 몬스터 AI·스킬·물약 단축키가 멈춘다")]
    public bool isLive = true;

    // GameManager가 없는 씬(단독 테스트 등)에서도 몬스터가 멈추지 않도록 없으면 진행 중으로 본다
    public static bool IsLive => instance == null || instance.isLive;

    // CoreSystem 오브젝트에 함께 붙어 있으므로 중복이면 이 컴포넌트만 제거 (오브젝트 전체를 지우면 형제 매니저까지 사라짐)
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
