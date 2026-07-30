using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    public bool isInBossRoom = false;

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    public bool CanSave    => !isInBossRoom;
    public bool HasSaveData => File.Exists(SavePath);

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────
    // 세이브
    // ─────────────────────────────────────────────────────────────────

    public bool Save(bool playSound = true)
    {
        if (!CanSave)
        {
            Debug.Log("[SaveManager] 보스 룸에서는 세이브할 수 없습니다.");
            return false;
        }

        if (DataManager.instance == null) return false;

        try
        {
            GameSaveData data = DataManager.instance.GetSaveData();

            // 포션 퀵슬롯
            if (PotionQuickSlotManager.instance != null)
            {
                data.hpPotionSlotItemId = PotionQuickSlotManager.instance
                    .GetSlot(ConsumableType.HpPotion)?.data?.itemId ?? "";
                data.mpPotionSlotItemId = PotionQuickSlotManager.instance
                    .GetSlot(ConsumableType.MpPotion)?.data?.itemId ?? "";
            }

            // NPC 재고
            data.npcStocks.Clear();
            foreach (var npc in NpcInteractable.AllInstances)
                data.npcStocks.Add(npc.GetStockSave());

            // 퀘스트 진행 상황
            QuestManager.instance?.FillSaveData(data);

            // 씬 / 위치
            data.sceneName = SceneManager.GetActiveScene().name;
            data.partyPositions.Clear();
            if (PartyManager.instance != null)
                foreach (var member in PartyManager.instance.partyMembers)
                    data.partyPositions.Add(member != null ? member.transform.position : Vector3.zero);

            if (PartyManager.instance?.cameraFollowTarget != null)
                data.cameraPosition = PartyManager.instance.cameraFollowTarget.transform.position;

            // 스킬 퀵슬롯 / 쿨타임
            data.quickSlots.Clear();
            if (PartyManager.instance != null)
            {
                foreach (var member in PartyManager.instance.partyMembers)
                {
                    var sm    = member?.GetComponent<SkillManager>();
                    var entry = new CharacterQuickSlotSave();
                    if (sm != null)
                    {
                        entry.slot0 = sm.GetSlot(0)?.skillData?.skillId ?? "";
                        entry.slot1 = sm.GetSlot(1)?.skillData?.skillId ?? "";
                        entry.slot2 = sm.GetSlot(2)?.skillData?.skillId ?? "";
                        entry.slot3 = sm.GetSlot(3)?.skillData?.skillId ?? "";
                        entry.cd0   = sm.GetCooldownRemaining(0);
                        entry.cd1   = sm.GetCooldownRemaining(1);
                        entry.cd2   = sm.GetCooldownRemaining(2);
                        entry.cd3   = sm.GetCooldownRemaining(3);
                    }
                    data.quickSlots.Add(entry);
                }
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveManager] 세이브 완료: {SavePath}");
            if (playSound) AudioManager.instance?.PlaySFX("SaveComplete");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 세이브 실패: {e.Message}");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 로드
    // ─────────────────────────────────────────────────────────────────

    public bool Load()
    {
        if (!HasSaveData)
        {
            Debug.Log("[SaveManager] 세이브 파일이 없습니다.");
            return false;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
            DataManager.instance?.LoadSaveData(data);

            QuestManager.instance?.RestoreProgress(data);
            StartCoroutine(RestorePosition(data));

            Debug.Log("[SaveManager] 로드 완료");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 로드 실패: {e.Message}");
            return false;
        }
    }

    private void RestorePotionSlots(GameSaveData data)
    {
        if (PotionQuickSlotManager.instance == null || DataManager.instance == null) return;

        foreach (var item in DataManager.instance.sharedInventory.Items)
        {
            if (item?.data == null) continue;
            if (item.data.itemId == data.hpPotionSlotItemId)
                PotionQuickSlotManager.instance.RegisterPotion(item);
            else if (item.data.itemId == data.mpPotionSlotItemId)
                PotionQuickSlotManager.instance.RegisterPotion(item);
        }
    }

    private IEnumerator RestorePosition(GameSaveData data)
    {
        if (string.IsNullOrEmpty(data.sceneName)) yield break;

        // 씬 전환은 호출자(TitleMenuUI 등)가 담당 — 여기서는 씬이 준비될 때까지만 대기
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == data.sceneName);
        yield return null; // 오브젝트 초기화 1프레임 대기

        var party = PartyManager.instance?.partyMembers;

        // 파티 위치/카메라 복원은 partyPositions가 있을 때만 — 구버전 세이브 등으로 비어있어도
        // 퀵슬롯/NPC재고/포션슬롯 복원은 서로 무관하므로 계속 진행해야 함
        if (party != null && data.partyPositions.Count > 0)
        {
            for (int i = 0; i < party.Count && i < data.partyPositions.Count; i++)
            {
                if (party[i] == null) continue;
                Vector3 pos = data.partyPositions[i];

                NavMeshAgent agent = party[i].GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                    agent.Warp(pos);
                else
                    party[i].transform.position = pos;
            }

            // 캐릭터 위치만 옮기고 카메라를 그대로 두면, 카메라가 원래 있던 곳(씬 시작 시 리더
            // 위치)에서 로드된 위치까지 서서히 따라오면서 화면이 부자연스럽게 흐르므로 함께 순간이동.
            // cameraPosition이 저장되기 전(구버전) 세이브 파일은 기본값 Vector3.zero이므로,
            // 그 경우엔 화면 원점으로 튀지 않도록 방금 복원한 리더 위치로 대신 스냅
            Vector3 camPos = data.cameraPosition != Vector3.zero
                ? data.cameraPosition
                : PartyManager.instance?.currentLeader?.transform.position ?? Vector3.zero;
            PartyManager.instance?.SnapCameraToPosition(camPos);
        }

        RestoreQuickSlots(data);
        RestoreNpcStocks(data);
        RestorePotionSlots(data);
    }

    private void RestoreNpcStocks(GameSaveData data)
    {
        if (data.npcStocks == null || data.npcStocks.Count == 0) return;
        var allNpcs = NpcInteractable.AllInstances;
        foreach (var stockSave in data.npcStocks)
        {
            foreach (var npc in allNpcs)
            {
                if (npc.NpcId == stockSave.npcId)
                {
                    npc.RestoreStock(stockSave);
                    break;
                }
            }
        }
    }

    private void RestoreQuickSlots(GameSaveData data)
    {
        if (PartyManager.instance == null || DataManager.instance == null) return;
        var party = PartyManager.instance.partyMembers;

        for (int i = 0; i < party.Count && i < data.quickSlots.Count; i++)
        {
            var sm    = party[i]?.GetComponent<SkillManager>();
            if (sm == null) continue;

            var    entry    = data.quickSlots[i];
            string[] ids    = { entry.slot0, entry.slot1, entry.slot2, entry.slot3 };
            float[]  cds    = { entry.cd0,   entry.cd1,   entry.cd2,   entry.cd3   };

            for (int j = 0; j < 4; j++)
            {
                if (string.IsNullOrEmpty(ids[j])) continue;
                SkillData sd = DataManager.instance.FindSkillById(ids[j]);
                if (sd == null) continue;

                sm.SetSlot(j, sd);
                if (cds[j] > 0f)
                    sm.GetSlot(j)?.SetCooldown(cds[j]);
            }
        }

        // 전투 퀵슬롯 UI 갱신
        CombatQuickSlotUI.instance?.RefreshSlots();
    }

    // ─────────────────────────────────────────────────────────────────
    // 삭제
    // ─────────────────────────────────────────────────────────────────

    public void DeleteSave()
    {
        if (HasSaveData) File.Delete(SavePath);
    }
}
