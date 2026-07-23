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
    [Tooltip("호버한 슬롯 기준 팝업 오프셋 (기본: 슬롯 위쪽)")]
    [SerializeField] Vector2 offset = new Vector2(0f, 90f);

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

    // 팝업이 슬롯과 다른 부모/캔버스 아래 있어도 안전하게 위치시키기 위해
    // 월드 좌표 → 스크린 좌표 → 팝업 부모 기준 로컬 좌표로 변환
    private void Reposition(RectTransform anchor)
    {
        if (panel == null || anchor == null) return;
        if (panel.parent is not RectTransform parentRect) return;

        Camera cam = panel.GetComponentInParent<Canvas>()?.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, anchor.position);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out Vector2 localPoint))
            panel.anchoredPosition = localPoint + offset;
    }

    private static float GetArrayValue(float[] arr, int level)
    {
        if (arr == null || arr.Length == 0) return 0f;
        int idx = Mathf.Clamp(level - 1, 0, arr.Length - 1);
        return arr[idx];
    }
}
