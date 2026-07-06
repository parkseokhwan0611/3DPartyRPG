using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 슬롯에 붙는 드래그 핸들러.
/// 장비 아이템만 드래그 가능 — 비장비 아이템은 즉시 취소.
/// </summary>
public class InventoryDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public ItemInstance item;
    [HideInInspector] public InventoryUI  inventoryUI;

    private GameObject _ghost;
    private Canvas     _canvas;

    // 더블클릭으로도 장착(드래그·장착 버튼과 동일한 결과). 슬롯은 자동 결정(반지는 1→2 순서).
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount < 2) return;
        if (item == null || !item.IsEquipment) return;
        inventoryUI?.EquipItem(item);
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (item == null || !item.IsEquipment)
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
}
