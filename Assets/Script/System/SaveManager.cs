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

    public bool Save()
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

            // 씬 / 위치
            data.sceneName = SceneManager.GetActiveScene().name;
            var leader = PartyManager.instance?.currentLeader;
            if (leader != null)
                data.playerPosition = leader.transform.position;

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveManager] 세이브 완료: {SavePath}");
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

            RestorePotionSlots(data);
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
        // 저장된 씬이 다르면 씬 전환 후 대기
        if (!string.IsNullOrEmpty(data.sceneName)
            && data.sceneName != SceneManager.GetActiveScene().name)
        {
            SceneManager.LoadScene(data.sceneName);
            yield return new WaitUntil(
                () => SceneManager.GetActiveScene().name == data.sceneName);
            yield return null; // 오브젝트 초기화 1프레임 대기
        }

        // 파티 리더 위치 복원 (NavMeshAgent.Warp로 NavMesh 위에 정확히 배치)
        if (data.playerPosition == Vector3.zero) yield break;

        var leader = PartyManager.instance?.currentLeader;
        if (leader == null) yield break;

        NavMeshAgent agent = leader.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(data.playerPosition);
        else
            leader.transform.position = data.playerPosition;
    }

    // ─────────────────────────────────────────────────────────────────
    // 삭제
    // ─────────────────────────────────────────────────────────────────

    public void DeleteSave()
    {
        if (HasSaveData) File.Delete(SavePath);
    }
}
