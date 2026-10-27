using UnityEngine;

public class DrawPile_3D : MonoBehaviour
{
    [Header("References")]
    public DeckManager_3D deckManager;

    private void OnMouseDown()
    {
        // Falls im Inspector nicht zugewiesen, automatisch suchen
        if (deckManager == null)
        {
            deckManager = Object.FindAnyObjectByType<DeckManager_3D>();
        }

        if (deckManager != null)
        {
            // Ruft die Logik im DeckManager auf (inklusive Nachzieh-Sperre & Drag-Spawn)
            deckManager.OnDeckClicked();
        }
        else
        {
            Debug.LogWarning("Kein DeckManager_3D in der Szene gefunden!");
        }
    }
}