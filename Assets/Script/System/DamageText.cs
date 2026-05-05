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

    void Awake()
    {
        text = GetComponent<TextMeshPro>();
    }

    // 색상 없는 버전 (몬스터 데미지텍스트 등 기존 호환용)
    public void Setup(float damageAmount)
    {
        Setup(damageAmount, Color.white);
    }

    // 색상 있는 버전 (플레이어 직업별 색상)
    public void Setup(float damageAmount, Color color)
    {
        if (text == null) text = GetComponent<TextMeshPro>();

        if (text == null)
        {
            Debug.LogError($"{gameObject.name} 프리팹에 TextMeshPro 컴포넌트가 없습니다!");
            return;
        }

        text.text  = ((int)damageAmount).ToString(); // 소수점 제거
        alpha      = color;
        alpha.a    = 1f;
        text.color = alpha;

        isInitialized = true;

        CancelInvoke("DestroyObject");
        Invoke("DestroyObject", destroyTime);
    }

    void Update()
    {
        if (!isInitialized || text == null) return;

        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);

        alpha.a    = Mathf.Lerp(alpha.a, 0, Time.deltaTime * alphaSpeed);
        text.color = alpha;
    }

    private void DestroyObject()
    {
        Destroy(gameObject);
    }
}