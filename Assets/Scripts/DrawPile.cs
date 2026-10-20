using UnityEngine;

public class DrawPile : MonoBehaviour
{
    public DeckManager3D deckManager;

    private void OnMouseDown()
    {
        if (deckManager != null)
        {
            deckManager.DrawCardToHand();
        }
    }
}
