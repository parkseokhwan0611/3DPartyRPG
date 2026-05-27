using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MenuTabUI : MonoBehaviour
{
    [Header("# 메뉴 창 루트")]
    public GameObject menuWindow;

    [Header("# 패널들")]
    public GameObject statWindow;
    public GameObject itemWindow;
    public GameObject skillWindow;
    public GameObject questWindow;
    public GameObject settingWindow;

    [Header("# 탭 버튼들 (하이라이트용)")]
    public Button statButton;
    public Button itemButton;
    public Button skillButton;
    public Button questButton;
    public Button settingButton;

    [Header("# 애니메이터 (Inspector에서 직접 연결)")]
    public Animator menuAnim;
    public Animator statAnim;
    public Animator itemAnim;
    public Animator skillAnim;
    public Animator questAnim;
    public Animator settingAnim;

    [Header("# 애니메이션 설정")]
    [Tooltip("Panel Out 애니메이션 길이 (초) — 이 시간 후 오브젝트 비활성화")]
    public float panelOutDuration = 0.3f;

    private const string ANIM_IN  = "Panel In";
    private const string ANIM_OUT = "Panel Out";

    private GameObject currentPanel;
    private Animator   currentPanelAnim;
    private Coroutine  closeMenuCoroutine;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (statButton != null)    statButton.onClick.AddListener(() => ShowPanel(statWindow,       statAnim));
        if (itemButton != null)    itemButton.onClick.AddListener(() => ShowPanel(itemWindow,       itemAnim));
        if (skillButton != null)   skillButton.onClick.AddListener(() => ShowPanel(skillWindow,     skillAnim));
        if (questButton != null)   questButton.onClick.AddListener(() => ShowPanel(questWindow,     questAnim));
        if (settingButton != null) settingButton.onClick.AddListener(() => ShowPanel(settingWindow, settingAnim));

        // 시작 시 메뉴 닫기 (애니메이션 없이 즉시)
        if (menuWindow != null) menuWindow.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleMenu();
    }

    // ─────────────────────────────────────────────────────────────────
    // 메뉴 토글
    // ─────────────────────────────────────────────────────────────────

    private void ToggleMenu()
    {
        if (!menuWindow.activeSelf)
            OpenMenu();
        else
            CloseMenu();
    }

    private void OpenMenu()
    {
        if (closeMenuCoroutine != null)
        {
            StopCoroutine(closeMenuCoroutine);
            closeMenuCoroutine = null;
        }

        menuWindow.SetActive(true);
        PlayAnim(menuAnim, ANIM_IN);

        // 기본 스탯 창 표시
        ShowPanel(statWindow, statAnim);
    }

    private void CloseMenu()
    {
        PlayAnim(menuAnim, ANIM_OUT);
        closeMenuCoroutine = StartCoroutine(DeactivateAfter(menuWindow, panelOutDuration));
    }

    // ─────────────────────────────────────────────────────────────────
    // 패널 전환 (Out/In 동시 재생)
    // ─────────────────────────────────────────────────────────────────

    public void ShowPanel(GameObject panel, Animator anim)
    {
        if (panel == null || panel == currentPanel) return;

        // 현재 패널 Out (동시에 시작)
        if (currentPanel != null)
        {
            PlayAnim(currentPanelAnim, ANIM_OUT);
            StartCoroutine(DeactivateAfter(currentPanel, panelOutDuration));
        }

        // 새 패널 In
        panel.SetActive(true);
        PlayAnim(anim, ANIM_IN);
        currentPanel     = panel;
        currentPanelAnim = anim;
    }

    // ─────────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────────

    private void PlayAnim(Animator anim, string stateName)
    {
        if (anim != null)
            anim.Play(stateName);
    }

    private IEnumerator DeactivateAfter(GameObject target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null)
            target.SetActive(false);
    }
}