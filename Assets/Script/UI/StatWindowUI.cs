using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatWindowUI : MonoBehaviour
{
    [Header("# 캐릭터 탭 버튼 (인덱스 순)")]
    public Button[] charTabButtons;

    [Header("# 좌측 프로필 (선택 캐릭터)")]
    public Image portraitImage;
    [Tooltip("charTabButtons와 같은 인덱스 순서로 캐릭터 초상화 스프라이트를 등록")]
    public Sprite[] charPortraits;
    public Image hpBarImage;
    public Image mpBarImage;
    public Image expBarImage;
    public TextMeshProUGUI hpBarValueText;
    public TextMeshProUGUI mpBarValueText;
    public TextMeshProUGUI expBarValueText;

    [Header("# 스탯 포인트")]
    public TextMeshProUGUI statPointText;

    [Header("# 기본 스탯 텍스트")]
    public TextMeshProUGUI strText;
    public TextMeshProUGUI vitText;
    public TextMeshProUGUI intText;
    public TextMeshProUGUI fthText;
    public TextMeshProUGUI dexText;

    [Header("# 스탯 올리기 버튼")]
    public Button strButton;
    public Button vitButton;
    public Button intButton;
    public Button fthButton;
    public Button dexButton;

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

    [Header("# 스킬 증감 표기 색상")]
    [Tooltip("버프/패시브로 오른 수치 (+N) 색상")]
    public Color skillBonusColor   = new Color32(0x7C, 0xFC, 0x7C, 0xFF);
    [Tooltip("감소 디버프로 깎인 수치 (-N) 색상")]
    public Color skillPenaltyColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);

    private int selectedIndex = 0;
    private Dictionary<int, CharacterStat> statCache = new Dictionary<int, CharacterStat>();

    // 창이 열려 있는 동안 버프가 걸리거나 끝나면 수치를 다시 그리기 위해 구독한 핸들러들
    private readonly List<PartyStatusEffectHandler> subscribedHandlers = new List<PartyStatusEffectHandler>();

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
        if (dexButton != null) dexButton.onClick.AddListener(() => AddStat(StatType.Dex));
    }

    void OnEnable()
    {
        BuildStatCache();
        SubscribeBuffEvents();

        // 스탯창은 열 때마다 항상 현재 조작 중인 리더 기준으로 시작
        selectedIndex = GetLeaderPartyIndex();
        if (DataManager.instance != null)
            DataManager.instance.selectedPartyIndex = selectedIndex;

        Refresh();
    }

    void OnDisable()
    {
        UnsubscribeBuffEvents();
    }

    private void SubscribeBuffEvents()
    {
        UnsubscribeBuffEvents();
        foreach (var cs in statCache.Values)
        {
            var handler = cs != null ? cs.GetComponent<PartyStatusEffectHandler>() : null;
            if (handler == null) continue;
            handler.OnBuffChanged += HandleBuffChanged;
            subscribedHandlers.Add(handler);
        }
    }

    private void UnsubscribeBuffEvents()
    {
        foreach (var handler in subscribedHandlers)
        {
            if (handler != null) handler.OnBuffChanged -= HandleBuffChanged;
        }
        subscribedHandlers.Clear();
    }

    private void HandleBuffChanged(StatusEffectType type, bool active) => Refresh();

    private int GetLeaderPartyIndex()
    {
        if (PartyManager.instance?.currentLeader == null) return 0;
        var stat = PartyManager.instance.currentLeader.GetComponent<CharacterStat>();
        return stat != null ? stat.partyIndex : 0;
    }

    private void BuildStatCache()
    {
        statCache.Clear();
        if (PartyManager.instance == null) return;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member == null) continue;
            var cs = member.GetComponent<CharacterStat>();
            if (cs != null) statCache[cs.partyIndex] = cs;
        }
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

    private enum StatType { Str, Vit, Int, Fth, Dex }

    private void AddStat(StatType type)
    {
        CharacterStatus status = GetStatus(selectedIndex);
        if (status == null || status.statPoint <= 0) return;

        float oldMaxHp = status.MaxHp;

        status.statPoint--;
        switch (type)
        {
            case StatType.Str: status.addedStr++; break;
            case StatType.Vit: status.addedVit++; break;
            case StatType.Int: status.addedInt++; break;
            case StatType.Fth: status.addedFht++; break;
            case StatType.Dex: status.addedDex++; break;
        }

        // VIT로 최대체력이 오른 만큼 현재체력도 같은 폭으로 조정
        if (type == StatType.Vit)
        {
            float delta = status.MaxHp - oldMaxHp;
            if (delta != 0f) status.currentHp = Mathf.Max(0f, status.currentHp + delta);
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
        SetText(dexText, $"민첩: {status.TotalDex:F0}");

        // ── + 버튼 활성/비활성 ──
        bool canSpend = status.statPoint > 0;
        SetButtonInteractable(strButton, canSpend);
        SetButtonInteractable(vitButton, canSpend);
        SetButtonInteractable(intButton, canSpend);
        SetButtonInteractable(fthButton, canSpend);
        SetButtonInteractable(dexButton, canSpend);

        // ── 전투 수치 ──
        if (charStat != null)
        {
            // "150 (+30)" — 150이 최종 수치, (+30)이 버프·패시브로 오른 양 (디버프로 깎이면 빨간 -N)
            SetText(phyAtkText,  $"물리 공격력: {WithSkillBonus(status.TotalAtk,      status.BaseAtk)}");
            SetText(apText,      $"마법 공격력: {WithSkillBonus(status.TotalAp,       status.BaseAp)}");
            SetText(defText,     $"방어력: {WithSkillBonus(status.TotalDef,           status.BaseDef)}");
            SetText(mresText,    $"마법 저항력: {WithSkillBonus(status.TotalMagicRes, status.BaseMagicRes)}");
            SetText(hpText,      $"체력: {charStat.Hp:F0} / {WithSkillBonus(status.MaxHp, status.BaseMaxHp)}");
            SetText(hpRegenText, $"체력 재생: {status.TotalHpRegen:F1} / 초");
            SetText(mpText,      $"마나: {charStat.Mp:F0} / {charStat.MaxMp:F0}");
            SetText(mpRegenText, $"마나 재생: {status.TotalMpRegen:F1} / 초");
            SetText(critText,    $"치명타 확률: {charStat.TotalCritRate * 100f:F1}%");
            SetText(cdmgText,    $"치명타 데미지: {charStat.TotalCritDamage * 100f:F1}%");

            if (hpBarImage != null) hpBarImage.fillAmount = charStat.MaxHp > 0f ? charStat.Hp / charStat.MaxHp : 0f;
            if (mpBarImage != null) mpBarImage.fillAmount = charStat.MaxMp > 0f ? charStat.Mp / charStat.MaxMp : 0f;
            SetText(hpBarValueText, $"{charStat.Hp:F0} / {charStat.MaxHp:F0}");
            SetText(mpBarValueText, $"{charStat.Mp:F0} / {charStat.MaxMp:F0}");
        }

        // ── 좌측 프로필 ──
        RefreshPortrait();
        RefreshExpBar();
    }

    private void RefreshPortrait()
    {
        if (portraitImage == null || charPortraits == null) return;
        if (selectedIndex < 0 || selectedIndex >= charPortraits.Length) return;

        Sprite sprite = charPortraits[selectedIndex];
        if (sprite != null) portraitImage.sprite = sprite;
    }

    // 경험치는 파티원 개인이 아니라 파티 전체 공유 수치 (LevelExpUI와 동일 기준)
    private void RefreshExpBar()
    {
        if (expBarImage == null || DataManager.instance == null) return;

        var dm = DataManager.instance;
        int required = dm.GetRequiredExp(dm.partyLevel);
        expBarImage.fillAmount = required > 0 ? (float)dm.partyExp / required : 0f;
        SetText(expBarValueText, $"{dm.partyExp} / {required}");
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
        => statCache.TryGetValue(partyIndex, out var cs) ? cs : null;

    // 최종 수치와 기본 수치(스탯 + 장비)를 각각 반올림한 뒤 차이를 표기 — 화면에 보이는 두 숫자의
    // 합이 항상 맞아떨어지게 (150.4와 120.6을 그대로 빼면 "150 (+30)"인데 기본이 121로 보이는 문제 방지)
    private string WithSkillBonus(float total, float baseValue)
    {
        int shownTotal = Mathf.RoundToInt(total);
        int diff       = shownTotal - Mathf.RoundToInt(baseValue);
        if (diff == 0) return shownTotal.ToString();

        string color = ColorUtility.ToHtmlStringRGB(diff > 0 ? skillBonusColor : skillPenaltyColor);
        return $"{shownTotal} <color=#{color}>({diff:+0;-0})</color>";
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
