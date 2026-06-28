using UnityEngine;
using UnityEngine.EventSystems;

public class HoverLine : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] GameObject selectedLine;

    void Start()
    {
        if (selectedLine != null)
            selectedLine.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectedLine != null)
            selectedLine.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (selectedLine != null)
            selectedLine.SetActive(false);
    }
}
