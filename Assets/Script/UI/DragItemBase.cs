using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// EnhancementDragItem / InventoryDragItem 공통 로직 — 드래그 고스트 생성/이동/제거.
// 클릭 시 동작(등록/장착)과 드래그 가능 조건은 하위 클래스에서 정의.
public abstract class DragItemBase : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public ItemInstance item;

    private GameObject _ghost;
    private Canvas     _canvas;

    public abstract void OnPointerClick(PointerEventData eventData);

    protected virtual bool CanDrag(ItemInstance item) => item != null && item.IsEquipment;

    public void OnBeginDrag(PointerEventData data)
    {
        if (!CanDrag(item))
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
