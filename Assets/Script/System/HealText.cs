using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshPro))]
[RequireComponent(typeof(PoolAble))]
public class HealText : MonoBehaviour
{
    public float moveSpeed  = 2f;
    public float alphaSpeed = 1.5f;
    public float lifetime   = 1f;

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

    public void Setup(float amount, Color color)
    {
        if (tmp == null) tmp = GetComponent<TextMeshPro>();

        tmp.text     = $"+{(int)amount}";
        currentColor = color;
        currentColor.a = 1f;
        tmp.color    = currentColor;

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

        if (timer <= 0f)
        {
            isActive = false;
            poolAble.ReleaseObject();
        }
    }
}
