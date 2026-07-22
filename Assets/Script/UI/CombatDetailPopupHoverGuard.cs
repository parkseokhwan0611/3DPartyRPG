using UnityEngine;
using UnityEngine.EventSystems;

// CombatDetailPopupUI의 panel(스킬 디테일 판넬) 오브젝트에 직접 붙이는 컴포넌트.
// 슬롯에서 벗어나 팝업 쪽으로 마우스를 옮기면(스크롤하려고 등) 예약된 숨김을 취소하고,
// 팝업 자체에서도 완전히 벗어나면 그때 숨김을 예약한다.
public class CombatDetailPopupHoverGuard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData) => CombatDetailPopupUI.instance?.CancelHide();
    public void OnPointerExit(PointerEventData eventData)  => CombatDetailPopupUI.instance?.RequestHide();
}
