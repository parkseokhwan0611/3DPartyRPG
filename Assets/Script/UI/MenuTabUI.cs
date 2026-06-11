using UnityEngine;
using UnityEngine.UI;

public class MenuTabUI : MonoBehaviour
{
    [Header("# 메뉴 창 루트")]
    public GameObject menuWindow; // Inventory 오브젝트

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

    // 메뉴 열림 여부 (PartyManager 등에서 입력 차단용)
    public static bool IsOpen { get; private set; }

    // 현재 열린 패널
    private GameObject currentPanel;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // 버튼 이벤트 연결
        if (statButton != null)    statButton.onClick.AddListener(() => ShowPanel(statWindow));
        if (itemButton != null)    itemButton.onClick.AddListener(() => ShowPanel(itemWindow));
        if (skillButton != null)   skillButton.onClick.AddListener(() => ShowPanel(skillWindow));
        if (questButton != null)   questButton.onClick.AddListener(() => ShowPanel(questWindow));
        if (settingButton != null) settingButton.onClick.AddListener(() => ShowPanel(settingWindow));

        // 시작 시 메뉴 닫기
        if (menuWindow != null) menuWindow.SetActive(false);
    }

    void Update()
    {
        if (menuWindow == null) return;

        // 탭키로 메뉴 토글
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleMenu();

        // ESC로 메뉴 닫기
        if (Input.GetKeyDown(KeyCode.Escape) && menuWindow.activeSelf)
            CloseMenu();
    }

    void OnDestroy()
    {
        // 씬 리로드·오브젝트 파괴 시 static 플래그 초기화
        // (메뉴 열린 채 씬 전환되어도 다음 씬에서 입력 차단 방지)
        IsOpen = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // 메뉴 토글
    // ─────────────────────────────────────────────────────────────────

    private void ToggleMenu()
    {
        bool isOpen = menuWindow.activeSelf;

        if (!isOpen)
            OpenMenu();
        else
            CloseMenu();
    }

    private void OpenMenu()
    {
        IsOpen = true;
        menuWindow.SetActive(true);
        ShowPanel(statWindow);
    }

    private void CloseMenu()
    {
        IsOpen = false;
        menuWindow.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // 패널 전환
    // ─────────────────────────────────────────────────────────────────

    public void ShowPanel(GameObject panel)
    {
        // 모든 패널 비활성화
        if (statWindow != null)    statWindow.SetActive(false);
        if (itemWindow != null)    itemWindow.SetActive(false);
        if (skillWindow != null)   skillWindow.SetActive(false);
        if (questWindow != null)   questWindow.SetActive(false);
        if (settingWindow != null) settingWindow.SetActive(false);

        // 선택한 패널만 활성화
        if (panel != null)
        {
            panel.SetActive(true);
            currentPanel = panel;
        }
    }
}