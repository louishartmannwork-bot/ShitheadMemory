using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Transform originalParent;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        transform.SetParent(canvas.transform, true);
        canvasGroup.blocksRaycasts = false;
        
        //also add the card being "hovered"
        //Make card bigger when dragging and bring to front 
    }
    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;

        //Make Card Smaller again

        if (dropTarget != null && dropTarget.name == "PileArea")
        {
            // Erfolgreich auf den Stapel gelegt!
            transform.SetParent(dropTarget.transform, true);
            //rectTransform.anchoredPosition = Vector2.zero; // Zentriert die Karte auf dem Stapel
            Debug.Log("Karte erfolgreich per Raycast auf PileArea gelegt!");
        }
        else
        {
            transform.SetParent(originalParent, true);
        }
    }
}
