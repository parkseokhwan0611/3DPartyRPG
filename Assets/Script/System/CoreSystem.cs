using UnityEngine;

// CoreSystem 프리팹 루트 — 씬이 바뀌어도 살아남는 매니저들(DataManager·SaveManager·QuestManager·AudioManager·
// SettingsManager·PotionQuickSlotManager·GameManager)을 하나로 묶고, 중복 처리를 여기서 한 번에 한다.
// 모든 씬에 CoreSystem이 놓여 있으므로, 이전 씬에서 넘어온 CoreSystem이 있으면 새 씬의 것은 중복이다.
// 가장 먼저 실행돼서 중복이면 오브젝트를 즉시 비활성화한다 — 비활성 오브젝트는 남은 형제 컴포넌트의
// Awake가 아예 불리지 않으므로, 각 매니저가 "나는 중복인가"를 따로 판단할 필요가 없다
// (각 매니저의 중복 검사는 이 컴포넌트가 빠진 오브젝트에 매니저를 단독으로 붙였을 때를 위한 안전장치로만 남김)
[DefaultExecutionOrder(-1000)]
public class CoreSystem : MonoBehaviour
{
    public static CoreSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
