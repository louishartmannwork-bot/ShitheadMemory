using UnityEngine;

public class DrawPile : MonoBehaviour
{
    public HandManager_3D handManager;

    private void OnMouseDown()
    {
        if (handManager != null)
        {
            handManager.DrawCardToPlayerHand();
        }
    }
}