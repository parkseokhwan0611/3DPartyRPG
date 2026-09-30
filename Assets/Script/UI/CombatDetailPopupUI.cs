using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 전투 UI 퀵슬롯(스킬 Q/W/E/R + 물약 HP/MP) 호버 시 뜨는 디테일 팝업.
// 슬롯마다 따로 만들지 않고 하나를 공유 — 동시에 하나만 보이면 되므로 재사용이 더 간단하고 가볍다.
public class CombatDetailPopupUI : MonoBehaviour
{
    public static CombatDetailPopupUI instance;
    public static bool IsOpen { get; private set; }

    [Header("패널")]
    [SerializeField] RectTransform    panel;
    [SerializeField] Image            iconImage;
    [SerializeField] TextMeshProUGUI  nameText;
    [Tooltip("이름 우측에 별도로 표시되는 쿨타임 텍스트 (롤 스타일)")]
    [SerializeField] TextMeshProUGUI  cooldownText;
    [SerializeField] TextMeshProUGUI  descText;
    [SerializeField] TextMeshProUGUI  statsText;
    [Tooltip("statsText가 들어있는 스크롤뷰 — 다른 슬롯 호버 시 맨 위로 리셋됨")]
    [SerializeField] ScrollRect       statsScroll;

    [Header("배치")]
    [Tooltip("팝업 아랫변 중앙을 호버한 슬롯 윗변 중앙에 붙인 뒤 더할 오프셋 (y = 슬롯과의 간격).\n" +
             "간격이 너무 크면 슬롯→팝업으로 마우스를 옮기는 사이 숨겨져 휠 스크롤을 못 하니 작게 유지")]
    [SerializeField] Vector2 offset = new Vector2(0f, 8f);

    private readonly Vector3[] _corners = new Vector3[4];

    [Tooltip("슬롯에서 벗어난 뒤 실제로 숨기기까지 대기 시간 — 그 사이 팝업(스크롤 영역 포함)으로 " +
             "마우스를 옮기면 숨김이 취소됨. panel 오브젝트에 CombatDetailPopupHoverGuard를 붙여야 동작")]
    [SerializeField] float hideDelay = 0.15f;

    private Coroutine _hideRoutine;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Hide();
    }

    void OnDestroy()
    {
        IsOpen = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 스킬 슬롯
    // ─────────────────────────────────────────────────────────────────

    public void ShowSkill(SkillBase skill, RectTransform anchor)
    {
        SkillData data = skill?.skillData;
        if (data == null || panel == null) return;

        CancelHide();

        // skill.skillLevel은 스킬을 실제로 "사용"할 때만 동기화되므로(SkillManager.SyncSkillLevel),
        // 레벨업 직후 아직 한 번도 안 썼다면 값이 갱신 전일 수 있음 — 캐릭터의 실시간 레벨을 직접 조회
        CharacterStat caster = PartyManager.instance?.currentLeader?.GetComponent<CharacterStat>();
        int level = caster != null ? Mathf.Max(1, caster.GetSkillLevel(data)) : Mathf.Max(1, skill.skillLevel);

        SetIconAndText(data.icon, $"Lv {level} {data.skillName}", data.description);
        if (cooldownText != null) cooldownText.text = $"{GetArrayValue(data.cooldown, level):F1}초";

        if (statsText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"MP 소모: {GetArrayValue(data.mpCost, level):F0}");

            // 데미지/치유량/버프 수치 — 전투 팝업은 계산식 없이 최종 수치만 표시
            string detail = SkillDescriptionBuilder.BuildCombatDescription(data, level, caster);
            if (!string.IsNullOrEmpty(detail))
                sb.AppendLine(detail);

            statsText.text = sb.ToString().TrimEnd();
        }

        ResetScroll();
        Reposition(anchor);
        panel.gameObject.SetActive(true);
        IsOpen = true;
    }

    // ─────────────────────────────────────────────────────────────────
    // 물약 슬롯
    // ─────────────────────────────────────────────────────────────────

    public void ShowPotion(ItemInstance item, RectTransform anchor)
    {
        if (item?.data == null || panel == null) return;

        CancelHide();

        SetIconAndText(item.data.icon, item.data.itemName, item.data.description);

        ConsumableData cd = item.data as ConsumableData;
        if (cooldownText != null) cooldownText.text = cd != null ? $"{cd.cooldown:F0}초" : "";

        if (statsText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"보유 수량: {item.stackCount}");
            if (cd != null)
                sb.AppendLine($"회복량: {cd.healAmount:F0}");
            statsText.text = sb.ToString().TrimEnd();
        }

        ResetScroll();
        Reposition(anchor);
        panel.gameObject.SetActive(true);
        IsOpen = true;
    }

    // 슬롯/팝업 양쪽 모두에서 벗어났을 때 호출 — 즉시 숨기지 않고 hideDelay만큼 대기해서,
    // 그 사이 슬롯→팝업으로 마우스가 이어지면(스크롤하려고 등) CancelHide로 취소되게 함
    public void RequestHide()
    {
        CancelHide();
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void CancelHide()
    {
        if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        _hideRoutine = null;
        Hide();
    }

    public void Hide()
    {
        CancelHide();
        if (panel != null) panel.gameObject.SetActive(false);
        IsOpen = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────────────────────────

    private void SetIconAndText(Sprite icon, string name, string desc)
    {
        if (iconImage != null)
        {
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }
        if (nameText != null) nameText.text = name;
        if (descText != null) descText.text = desc;
    }

    private void ResetScroll()
    {
        if (statsScroll != null) statsScroll.verticalNormalizedPosition = 1f;
    }

    // 호버한 슬롯 바로 위에 팝업을 붙이고, 화면(캔버스) 밖으로 나가지 않게 안쪽으로 밀어 넣는다.
    // 팝업이 슬롯과 다른 부모/캔버스 아래 있어도 되도록 월드 → 스크린 → 팝업 부모 로컬 좌표로 변환
    private void Reposition(RectTransform anchor)
    {
        if (panel == null || anchor == null) return;
        if (panel.parent is not RectTransform parentRect) return;

        // Overlay 캔버스는 카메라 없이 변환해야 한다 — 캔버스에 worldCamera가 지정돼 있어도 넘기면
        // 좌표가 엉뚱한 한 점으로 모여 어느 슬롯에서든 같은 자리에 뜬다
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        // 슬롯 윗변 중앙 (GetWorldCorners: 0 좌하, 1 좌상, 2 우상, 3 우하)
        anchor.GetWorldCorners(_corners);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, (_corners[1] + _corners[2]) * 0.5f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out Vector2 slotTop))
            return;

        // 피벗이 어디든 팝업 아랫변 중앙이 슬롯 윗변 중앙 + offset에 오도록
        Vector2 size  = panel.rect.size;
        Vector2 pivot = panel.pivot;
        Vector2 pos   = slotTop + offset + new Vector2((pivot.x - 0.5f) * size.x, pivot.y * size.y);

        // 캔버스 영역 안으로 클램프 (부모 로컬 좌표 기준)
        if (canvas != null && canvas.rootCanvas.transform is RectTransform rootRect)
        {
            rootRect.GetWorldCorners(_corners);
            Vector2 min = parentRect.InverseTransformPoint(_corners[0]);
            Vector2 max = parentRect.InverseTransformPoint(_corners[2]);
            pos.x = Mathf.Clamp(pos.x, min.x + pivot.x * size.x, max.x - (1f - pivot.x) * size.x);
            pos.y = Mathf.Clamp(pos.y, min.y + pivot.y * size.y, max.y - (1f - pivot.y) * size.y);
        }

        // localPosition은 부모 피벗 기준이라 팝업의 앵커 설정과 무관하게 정확히 맞는다
        panel.localPosition = new Vector3(pos.x, pos.y, panel.localPosition.z);
    }

    private static float GetArrayValue(float[] arr, int level)
    {
        if (arr == null || arr.Length == 0) return 0f;
        int idx = Mathf.Clamp(level - 1, 0, arr.Length - 1);
        return arr[idx];
    }
}
