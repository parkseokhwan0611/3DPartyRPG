using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// EnhancementUI 인벤토리 슬롯에 붙는 드래그 핸들러.
/// 장비와 강화 주문서 모두 드래그 가능.
/// </summary>
public class EnhancementDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public ItemInstance item;

    private GameObject _ghost;
    private Canvas     _canvas;

    public void OnBeginDrag(PointerEventData data)
    {
        if (item == null || (!item.IsEquipment && !IsEnhancementScroll(item)))
        {
            data.pointerDrag = null;
            return;
        }

        _canvas = _canvas != null ? _canvas : GetComponentInParent<Canvas>(true);
        if (_canvas == null) return;

        _ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _ghost.transform.SetParent(_canvas.transform, false);
        _ghost.transform.SetAsLastSibling();

        var img = _ghost.GetComponent<Image>();
        img.sprite        = item.data.icon;
        img.raycastTarget = false;

        _ghost.GetComponent<CanvasGroup>().blocksRaycasts = false;

        var rt = (RectTransform)_ghost.transform;
        rt.sizeDelta = new Vector2(60f, 60f);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        MoveGhost(data.position);
    }

    public void OnDrag(PointerEventData data) => MoveGhost(data.position);

    public void OnEndDrag(PointerEventData data)
    {
        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
    }

    private void MoveGhost(Vector2 screenPos)
    {
        if (_ghost == null || _canvas == null) return;
        var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_canvas.transform, screenPos, camera, out Vector2 local);
        ((RectTransform)_ghost.transform).anchoredPosition = local;
    }

    private static bool IsEnhancementScroll(ItemInstance item)
        => item.data is ConsumableData cd
        && (cd.consumableType == ConsumableType.WeaponScroll
         || cd.consumableType == ConsumableType.ArmorScroll);
}
