using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatWindowUI : MonoBehaviour
{
    [Header("# 캐릭터 탭 버튼 (인덱스 순)")]
    public Button[] charTabButtons;

    [Header("# 스탯 포인트")]
    public TextMeshProUGUI statPointText;

    [Header("# 기본 스탯 텍스트")]
    public TextMeshProUGUI strText;
    public TextMeshProUGUI vitText;
    public TextMeshProUGUI intText;
    public TextMeshProUGUI fthText;

    [Header("# 스탯 올리기 버튼")]
    public Button strButton;
    public Button vitButton;
    public Button intButton;
    public Button fthButton;

    [Header("# 전투 수치 텍스트")]
    public TextMeshProUGUI phyAtkText;
    public TextMeshProUGUI apText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI mresText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI hpRegenText;
    public TextMeshProUGUI mpText;
    public TextMeshProUGUI mpRegenText;
    public TextMeshProUGUI critText;
    public TextMeshProUGUI cdmgText;

    private int selectedIndex = 0;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // 캐릭터 탭 버튼 연결
        for (int i = 0; i < charTabButtons.Length; i++)
        {
            int idx = i;
            if (charTabButtons[i] != null)
                charTabButtons[i].onClick.AddListener(() => SelectChar(idx));
        }

        // 스탯 올리기 버튼 연결
        if (strButton != null) strButton.onClick.AddListener(() => AddStat(StatType.Str));
        if (vitButton != null) vitButton.onClick.AddListener(() => AddStat(StatType.Vit));
        if (intButton != null) intButton.onClick.AddListener(() => AddStat(StatType.Int));
        if (fthButton != null) fthButton.onClick.AddListener(() => AddStat(StatType.Fth));
    }

    void OnEnable()
    {
        // 인벤토리창 등 다른 창에서 선택한 캐릭터 유지
        if (DataManager.instance != null)
            selectedIndex = DataManager.instance.selectedPartyIndex;

        Refresh();
    }

    // ─────────────────────────────────────────────────────────────────
    // 캐릭터 선택
    // ─────────────────────────────────────────────────────────────────

    private void SelectChar(int index)
    {
        selectedIndex = index;
        if (DataManager.instance != null)
            DataManager.instance.selectedPartyIndex = index;
        Refresh();
    }

    // ─────────────────────────────────────────────────────────────────
    // 스탯 포인트 사용
    // ─────────────────────────────────────────────────────────────────

    private enum StatType { Str, Vit, Int, Fth }

    private void AddStat(StatType type)
    {
        CharacterStatus status = GetStatus(selectedIndex);
        if (status == null || status.statPoint <= 0) return;

        status.statPoint--;
        switch (type)
        {
            case StatType.Str: status.addedStr++; break;
            case StatType.Vit: status.addedVit++; break;
            case StatType.Int: status.addedInt++; break;
            case StatType.Fth: status.addedFht++; break;
        }

        // 스탯 변동 → 파티원 UI 갱신 통지
        CharacterStat charStat = GetCharStat(selectedIndex);
        // VIT: MaxHp 변동 / INT·FTH: TotalAp·MpRegen 변동
        if (type == StatType.Vit)             charStat?.RaiseHpChanged();
        else if (type == StatType.Int || type == StatType.Fth) charStat?.RaiseMpChanged();

        Refresh();
    }

    // ─────────────────────────────────────────────────────────────────
    // 전체 갱신
    // ─────────────────────────────────────────────────────────────────

    private void Refresh()
    {
        CharacterStatus status   = GetStatus(selectedIndex);
        CharacterStat   charStat = GetCharStat(selectedIndex);

        if (status == null) return;

        // ── 스탯 포인트 ──
        SetText(statPointText, $"스탯 포인트: {status.statPoint}");

        // ── 기본 스탯 수치 (장비 보너스 포함) ──
        SetText(strText, $"힘: {status.classData.baseStr + status.addedStr + status.equipStr:F0}");
        SetText(vitText, $"체력: {status.classData.baseVit + status.addedVit + status.equipVit:F0}");
        SetText(intText, $"지능: {status.classData.baseInt + status.addedInt + status.equipInt:F0}");
        SetText(fthText, $"신앙: {status.classData.baseFht + status.addedFht + status.equipFht:F0}");

        // ── + 버튼 활성/비활성 ──
        bool canSpend = status.statPoint > 0;
        SetButtonInteractable(strButton, canSpend);
        SetButtonInteractable(vitButton, canSpend);
        SetButtonInteractable(intButton, canSpend);
        SetButtonInteractable(fthButton, canSpend);

        // ── 전투 수치 ──
        if (charStat != null)
        {
            SetText(phyAtkText,  $"물리 공격력: {charStat.TotalAtk:F0}");
            SetText(apText,      $"마법 공격력: {charStat.TotalAp:F0}");
            SetText(defText,     $"방어력: {charStat.TotalDef:F0}");
            SetText(mresText,    $"마법 저항력: {charStat.TotalMagicRes:F0}");
            SetText(hpText,      $"체력: {charStat.Hp:F0} / {charStat.MaxHp:F0}");
            SetText(hpRegenText, $"체력 재생: {status.TotalHpRegen:F1} / 초");
            SetText(mpText,      $"마나: {charStat.Mp:F0} / {charStat.MaxMp:F0}");
            SetText(mpRegenText, $"마나 재생: {status.TotalMpRegen:F1} / 초");
            SetText(critText,    $"치명타 확률: {charStat.TotalCritRate * 100f:F1}%");
            SetText(cdmgText,    $"치명타 데미지: {charStat.TotalCritDamage * 100f:F1}%");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────────

    private CharacterStatus GetStatus(int partyIndex)
    {
        if (DataManager.instance == null) return null;
        if (partyIndex >= DataManager.instance.partyStatuses.Count) return null;
        return DataManager.instance.partyStatuses[partyIndex];
    }

    private CharacterStat GetCharStat(int partyIndex)
    {
        if (PartyManager.instance == null) return null;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            var stat = member.GetComponent<CharacterStat>();
            if (stat != null && stat.partyIndex == partyIndex)
                return stat;
        }
        return null;
    }

    private void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn != null) btn.interactable = interactable;
    }
}
