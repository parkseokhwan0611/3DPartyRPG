using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshPro))]
[RequireComponent(typeof(PoolAble))]
public class HealText : MonoBehaviour
{
    public float moveSpeed  = 2f;
    public float alphaSpeed = 1.5f;
    public float lifetime   = 1f;

    [Header("# 치명타 아이콘 (선택 — 비워두면 표시 안 함, 데미지 텍스트와 같은 아이콘)")]
    [SerializeField] SpriteRenderer critIcon;
    private Color critIconColor;

    private TextMeshPro tmp;
    private PoolAble poolAble;
    private Color currentColor;
    private float timer;
    private bool isActive;

    void Awake()
    {
        tmp      = GetComponent<TextMeshPro>();
        poolAble = GetComponent<PoolAble>();
    }

    void OnEnable()
    {
        timer    = lifetime;
        isActive = true;
    }

    void OnDisable()
    {
        isActive = false;
    }

    public void Setup(float amount, Color color, bool isCrit = false)
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();

        tmp.text     = $"+{(int)amount}";
        currentColor = color;
        currentColor.a = 1f;
        tmp.color    = currentColor;

        // 힐 치명타 — 데미지 텍스트와 같은 치명타 아이콘 (마나 회복 등은 항상 꺼짐)
        if (critIcon != null)
        {
            critIcon.gameObject.SetActive(isCrit);
            critIconColor   = critIcon.color;
            critIconColor.a = 1f;
            critIcon.color  = critIconColor;
        }

        timer    = lifetime;
        isActive = true;
    }

    void Update()
    {
        if (!isActive) return;

        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime, Space.World);

        timer -= Time.deltaTime;
        float t = 1f - Mathf.Clamp01(timer / lifetime);
        currentColor.a = Mathf.Lerp(1f, 0f, t);
        tmp.color      = currentColor;

        if (critIcon != null && critIcon.gameObject.activeSelf)
        {
            critIconColor.a = currentColor.a;
            critIcon.color  = critIconColor;
        }

        if (timer <= 0f)
        {
            isActive = false;
            poolAble.ReleaseObject();
        }
    }
}
