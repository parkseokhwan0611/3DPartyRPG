using UnityEngine;
using TMPro;

public class DamageText : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float alphaSpeed = 1f;
    public float destroyTime = 1f;

    private TextMeshPro text;
    private Color alpha;
    private bool isInitialized = false;
    private PoolAble poolAble;

    void Awake()
    {
        text     = GetComponent<TextMeshPro>();
        poolAble = GetComponent<PoolAble>(); // 없으면 null → Destroy fallback
    }

    public void Setup(float damageAmount)
    {
        Setup(damageAmount, Color.white);
    }

    public void Setup(float damageAmount, Color color, bool showMinus = false)
    {
        if (text == null) text = GetComponent<TextMeshPro>();

        if (text == null)
        {
            Debug.LogError($"{gameObject.name} 프리팹에 TextMeshPro 컴포넌트가 없습니다!");
            return;
        }

        string prefix = showMinus ? "-" : "";
        text.text  = prefix + ((int)damageAmount).ToString();
        alpha      = color;
        alpha.a    = 1f;
        text.color = alpha;

        isInitialized = true;

        CancelInvoke(nameof(Release));
        Invoke(nameof(Release), destroyTime);
    }

    void Update()
    {
        if (!isInitialized || text == null) return;

        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);

        alpha.a    = Mathf.Lerp(alpha.a, 0, Time.deltaTime * alphaSpeed);
        text.color = alpha;
    }

    private void Release()
    {
        isInitialized = false;
        if (poolAble != null)
            poolAble.ReleaseObject();
        else
            Destroy(gameObject);
    }
}
