using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour
{
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("OnDrop wurde aufgerufen von: " + eventData.pointerDrag.name);

        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            DraggableCard card = droppedObject.GetComponent<DraggableCard>();
            if (card != null)
            {
                droppedObject.transform.SetParent(transform, true);
                Debug.Log("Karte erfolgreich auf den Ablagestapel gelegt!");
            }
        }
    }
}
