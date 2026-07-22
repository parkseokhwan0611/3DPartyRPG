using UnityEngine;
using UnityEngine.UI;

// ShopUI 등 자체적으로 ESC를 처리하는 UI보다 먼저 Update가 돌아야
// IsOpen 플래그를 같은 프레임에 오독해서 설정창이 겹쳐 열리는 경합을 방지할 수 있음
[DefaultExecutionOrder(-100)]
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

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // 상점·강화 등이 열려있으면 Tab으로 해당 UI를 닫고 인벤토리는 열지 않음
            if (TryCloseBlockingUI()) return;
            ToggleMenu();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 강화 UI가 열려있으면 먼저 닫기
            if (EnhancementUI.IsOpen) { EnhancementUI.instance?.Close(); return; }

            if (menuWindow.activeSelf)
            {
                // 인벤토리 내 팝업 먼저 닫기, 없으면 메뉴 전체 닫기
                if (InventoryUI.instance != null && InventoryUI.instance.TryCloseSubPanel()) return;
                CloseMenu();
            }
            else if (!DialogueUI.IsOpen && !ShopUI.IsOpen)
            {
                // 게임 화면(메뉴 닫힌 상태)에서는 설정 창을 바로 띄움
                OpenMenuToSettings();
            }
        }
    }

    // 단독 UI가 열려있으면 닫고 true 반환 (Tab·ESC 입력 소비용)
    private static bool TryCloseBlockingUI()
    {
        if (DialogueUI.IsOpen) return true;  // 대화 중엔 Tab 무시
        if (ShopUI.IsOpen)        { ShopUI.instance?.Close();        return true; }
        if (EnhancementUI.IsOpen) { EnhancementUI.instance?.Close(); return true; }
        return false;
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
        AudioManager.instance?.PlaySFX("UIOpen");
    }

    private void OpenMenuToSettings()
    {
        IsOpen = true;
        menuWindow.SetActive(true);
        ShowPanel(settingWindow);
        AudioManager.instance?.PlaySFX("UIOpen");
    }

    private void CloseMenu()
    {
        IsOpen = false;
        menuWindow.SetActive(false);
        AudioManager.instance?.PlaySFX("UIClose");
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