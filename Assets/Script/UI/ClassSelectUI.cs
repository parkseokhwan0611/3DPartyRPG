using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 새 게임 클래스 선택 화면 — 캐릭터마다 한 칸씩, 클래스 버튼 두 개 중 하나를 고른다.
// 고르는 동안은 전시 모델의 무기·외형만 바꾸고(ClassDisplayModel.Preview), 확정할 때 고른 클래스로 새 게임 데이터를 다시 만든다
// (버튼·필드 이름의 weapon은 예전 "무기 선택" 시절 이름 — 씬에 저장된 값이 끊기지 않도록 그대로 둔다)
public class ClassSelectUI : MonoBehaviour
{
    [Serializable]
    public class WeaponButton
    {
        public Button button;
        [Tooltip("버튼 글자 (비워도 됨) — Weapon Name을 적으면 그 글자로 바꾼다")]
        public TMP_Text label;
        [Tooltip("무기 이름 — 버튼 글자와 칸의 무기 이름 텍스트에 표시. 비우면 버튼에 적힌 글자 그대로")]
        public string weaponName;
        [Tooltip("역할 태그 (예: 탱커, 근접 딜러, 서포터) — 칸의 Role Text에 표시. 비우면 표시 안 함")]
        public string role;
        [Tooltip("이 무기를 골랐을 때 칸의 설명 텍스트에 표시할 내용")]
        [TextArea(3, 6)]
        public string description;

        // 표시할 무기 이름 — Weapon Name → 버튼 글자 → 클래스 이름 순
        public string GetDisplayName(ClassData cls)
        {
            if (!string.IsNullOrEmpty(weaponName)) return weaponName;
            if (label != null && !string.IsNullOrEmpty(label.text)) return label.text;
            return cls != null ? cls.classType.ToString() : "";
        }
    }

    [Serializable]
    public class Column
    {
        [Tooltip("이 칸이 보여주는 파티 인덱스 (0=바루니우스, 1=솔라리스, 2=루나리스)")]
        public int partyIndex;
        [Tooltip("이 칸의 전시 모델 (Entries에 이 캐릭터의 두 클래스가 모두 등록돼 있어야 함)")]
        public ClassDisplayModel displayModel;
        public TMP_Text characterNameText;
        public TMP_Text weaponNameText;
        [Tooltip("역할 태그 텍스트 (비워두면 무기 이름 앞에 [역할]로 붙여서 표시)")]
        public TMP_Text roleText;
        public TMP_Text descriptionText;
        [Tooltip("0번 = 첫 번째 무기, 1번 = 두 번째 무기 (DataManager의 Base Data List / Second Class List 순서)")]
        public List<WeaponButton> weaponButtons = new List<WeaponButton>();

        [NonSerialized] public List<ClassData> options = new List<ClassData>();
        [NonSerialized] public int selected;
    }

    [Header("# 칸")]
    [SerializeField] List<Column> columns = new List<Column>();

    [Header("# 버튼")]
    [SerializeField] Button confirmButton;
    [SerializeField] Button backButton;

    [Header("# 선택 표시")]
    [Tooltip("선택된 무기 버튼 스프라이트")]
    [SerializeField] Sprite selectedSprite;
    [Tooltip("선택되지 않은 무기 버튼 스프라이트 (기본 상태)")]
    [SerializeField] Sprite normalSprite;

    [Header("# 씬")]
    [Tooltip("확정 후 이동할 씬")]
    [SerializeField] string nextSceneName  = "PrologueScene";
    [Tooltip("뒤로 가기(버튼·ESC) 시 이동할 씬")]
    [SerializeField] string titleSceneName = "TitleScene";

    private bool _isTransitioning;

    void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (backButton    != null) backButton.onClick.AddListener(OnBackClicked);

        if (DataManager.instance == null)
        {
            // 이 씬을 바로 실행한 경우 등 — 고를 클래스 목록이 없으므로 확정을 막는다
            Debug.LogWarning("[ClassSelectUI] DataManager가 없습니다. 타이틀 씬에서 새 게임으로 들어와야 합니다.");
            if (confirmButton != null) confirmButton.interactable = false;
            return;
        }

        foreach (var col in columns)
        {
            col.options = DataManager.instance.GetClassOptions(col.partyIndex);

            // 처음 선택 = 지금 데이터의 시작 클래스 (보통 첫 번째 무기)
            col.selected = 0;
            var statuses = DataManager.instance.partyStatuses;
            if (col.partyIndex >= 0 && col.partyIndex < statuses.Count)
            {
                int idx = col.options.IndexOf(statuses[col.partyIndex].classData);
                if (idx >= 0) col.selected = idx;
            }

            for (int i = 0; i < col.weaponButtons.Count; i++)
            {
                var wb = col.weaponButtons[i];
                if (wb?.button == null) continue;

                bool has = i < col.options.Count && col.options[i] != null;
                wb.button.gameObject.SetActive(has);
                if (!has) continue;

                if (wb.label != null && !string.IsNullOrEmpty(wb.weaponName)) wb.label.text = wb.weaponName;

                Column c = col;
                int optionIndex = i;
                wb.button.onClick.AddListener(() => Select(c, optionIndex));
            }

            Refresh(col);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) OnBackClicked();
    }

    private void Select(Column col, int optionIndex)
    {
        if (_isTransitioning || optionIndex < 0 || optionIndex >= col.options.Count) return;
        col.selected = optionIndex;
        Refresh(col);

        // 누른 버튼이 EventSystem에 선택 상태로 남아 하이라이트가 안 빠지는 것 방지
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void Refresh(Column col)
    {
        if (col.options.Count == 0) return;
        col.selected = Mathf.Clamp(col.selected, 0, col.options.Count - 1);
        ClassData    cls = col.options[col.selected];
        WeaponButton sel = col.selected < col.weaponButtons.Count ? col.weaponButtons[col.selected] : null;

        if (col.characterNameText != null)
            col.characterNameText.text = string.IsNullOrEmpty(cls.displayName) ? cls.name : cls.displayName;
        string weaponName = sel != null ? sel.GetDisplayName(cls) : cls.classType.ToString();
        string role       = sel != null ? sel.role : "";

        if (col.roleText != null)
            col.roleText.text = string.IsNullOrEmpty(role) ? "" : $"[{role}]";
        else if (!string.IsNullOrEmpty(role))
            weaponName = $"[{role}] {weaponName}"; // 역할 텍스트 칸이 없으면 무기 이름 앞에 붙임

        if (col.weaponNameText  != null) col.weaponNameText.text  = weaponName;
        if (col.descriptionText != null) col.descriptionText.text = sel != null ? sel.description : "";

        for (int i = 0; i < col.weaponButtons.Count; i++)
        {
            var wb = col.weaponButtons[i];
            if (wb?.button == null) continue;
            bool on = i == col.selected;
            Sprite sprite = on ? selectedSprite : normalSprite;
            if (wb.button.image != null && sprite != null) wb.button.image.sprite = sprite;
        }

        if (col.displayModel != null) col.displayModel.Preview(cls.classType);
    }

    private void OnConfirmClicked()
    {
        if (_isTransitioning || DataManager.instance == null) return;
        _isTransitioning = true;
        SetButtonsInteractable(false);

        // 파티 인덱스 순서로 고른 클래스 목록을 만든다 (칸이 없는 파티원은 기본 시작 클래스)
        int count = DataManager.instance.baseDataList.Count;
        var chosen = new ClassData.ClassType[count];
        var hasChoice = new bool[count];
        foreach (var col in columns)
        {
            if (col.partyIndex < 0 || col.partyIndex >= count || col.options.Count == 0) continue;
            chosen[col.partyIndex]    = col.options[Mathf.Clamp(col.selected, 0, col.options.Count - 1)].classType;
            hasChoice[col.partyIndex] = true;
        }
        for (int i = 0; i < count; i++)
        {
            if (hasChoice[i]) continue;
            var start = DataManager.instance.partyStatuses.Count > i ? DataManager.instance.partyStatuses[i].classData : null;
            var first = DataManager.instance.baseDataList[i];
            chosen[i] = start != null ? start.classType : (first != null ? first.classType : default);
        }

        DataManager.instance.InitData(chosen);
        SceneLoader.Load(nextSceneName);
    }

    private void OnBackClicked()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        SetButtonsInteractable(false);
        SceneLoader.Load(titleSceneName);
    }

    private void SetButtonsInteractable(bool value)
    {
        if (confirmButton != null) confirmButton.interactable = value;
        if (backButton    != null) backButton.interactable    = value;
        foreach (var col in columns)
            foreach (var wb in col.weaponButtons)
                if (wb?.button != null) wb.button.interactable = value;
    }
}
