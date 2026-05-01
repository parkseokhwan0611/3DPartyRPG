using UnityEngine;
using TMPro;

public class DamageText : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float alphaSpeed = 1f;
    public float destroyTime = 1f;
    
    private TextMeshProUGUI text;
    private Color alpha;
    private bool isInitialized = false;

    // Awake에서도 미리 찾아둡니다.
    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    public void Setup(float damageAmount)
    {
        // [중요] text가 null이라면 여기서 한 번 더 찾습니다.
        if (text == null) 
        {
            text = GetComponent<TextMeshProUGUI>();
        }

        // 만약 GetComponent를 했는데도 null이라면 프리팹 설정 문제 입니디.
        if (text == null)
        {
            Debug.LogError($"{gameObject.name} 프리팹에 TextMeshPro 컴포넌트가 없습니다!");
            return;
        }

        text.text = damageAmount.ToString();
        alpha = text.color;
        alpha.a = 1f;
        text.color = alpha;

        isInitialized = true;
        
        // Invoke 대신 확실하게 풀링이나 파괴 로직 연결
        CancelInvoke("DestroyObject");
        Invoke("DestroyObject", destroyTime);
    }

    void Update()
    {
        if (!isInitialized || text == null) return;

        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
        
        alpha.a = Mathf.Lerp(alpha.a, 0, Time.deltaTime * alphaSpeed);
        text.color = alpha;
    }

    private void DestroyObject()
    {
        // 현재는 Destroy를 사용하지만, 나중엔 Pool.Release(gameObject)로 바꾸세요!
        Destroy(gameObject);
    }
}