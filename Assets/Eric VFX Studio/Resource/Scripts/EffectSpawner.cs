using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 如果有安裝新版 Input System 就引用它
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class EffectToolManager : MonoBehaviour
{
    [Header("--- 特效設定 ---")]
    public List<GameObject> effectLibrary;
    public float destroyTime = 2.0f;
    public LayerMask clickLayer;
    public float spawnOffset = 0.5f; 
    public bool faceCamera = true;

    [Header("--- UI 內容 ---")]
    public Dropdown effectDropdown;

    private GameObject currentEffectPrefab;
    private int currentIndex = 0;

    void Start()
    {
        if (effectLibrary.Count > 0)
        {
            currentIndex = 0;
            currentEffectPrefab = effectLibrary[currentIndex];
            SetupDropdown();
        }
    }

    void SetupDropdown()
    {
        if (effectDropdown == null) return;
        effectDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (var prefab in effectLibrary) { options.Add(prefab.name); }
        effectDropdown.AddOptions(options);
        effectDropdown.value = currentIndex;
        effectDropdown.onValueChanged.AddListener(index => {
            currentIndex = index;
            currentEffectPrefab = effectLibrary[currentIndex];
        });
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        bool mouseClick = false;
        bool upPressed = false;
        bool downPressed = false;

        // --- 判斷目前專案使用哪種輸入系統 ---
#if ENABLE_INPUT_SYSTEM
        // 新版 Input System 語法
        if (Mouse.current != null) mouseClick = Mouse.current.leftButton.wasPressedThisFrame;
        if (Keyboard.current != null)
        {
            upPressed = Keyboard.current.upArrowKey.wasPressedThisFrame;
            downPressed = Keyboard.current.downArrowKey.wasPressedThisFrame;
        }
#else
        // 舊版 Input Manager 語法
        mouseClick = Input.GetMouseButtonDown(0);
        upPressed = Input.GetKeyDown(KeyCode.UpArrow);
        downPressed = Input.GetKeyDown(KeyCode.DownArrow);
#endif

        // 鍵盤切換邏輯
        if (upPressed) { currentIndex = (currentIndex - 1 + effectLibrary.Count) % effectLibrary.Count; UpdateSelection(); }
        if (downPressed) { currentIndex = (currentIndex + 1) % effectLibrary.Count; UpdateSelection(); }

        // 滑鼠點擊邏輯 (避開 UI)
        if (mouseClick && !EventSystem.current.IsPointerOverGameObject())
        {
            SpawnEffect();
        }
    }

    void UpdateSelection()
    {
        currentEffectPrefab = effectLibrary[currentIndex];
        if (effectDropdown != null)
        {
            effectDropdown.value = currentIndex;
            effectDropdown.RefreshShownValue();
        }
    }

    void SpawnEffect()
    {
        if (currentEffectPrefab == null) return;

        // 取得滑鼠位置 (相容新舊系統)
        Vector2 mousePos;
#if ENABLE_INPUT_SYSTEM
        mousePos = Mouse.current.position.ReadValue();
#else
        mousePos = Input.mousePosition;
#endif

        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickLayer))
        {
            Vector3 spawnPosition = hit.point + (hit.normal * spawnOffset);
            Quaternion spawnRotation = faceCamera ? Quaternion.LookRotation(Camera.main.transform.forward) : Quaternion.LookRotation(hit.normal);
            
            GameObject effect = Instantiate(currentEffectPrefab, spawnPosition, spawnRotation);
            Destroy(effect, destroyTime);
        }
    }
}