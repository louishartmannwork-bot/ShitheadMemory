using UnityEngine;
using System.Collections.Generic;
using ShitheadMemory;

public class PileManager_3D : MonoBehaviour
{
    public static PileManager_3D Instance;

    [Header("Discard Pile Storage")]
    [SerializeField] private List<Card> discardPileData = new List<Card>();
    [SerializeField] private List<GameObject> discardPileObjects = new List<GameObject>();

    [Header("Pile Transform / Layout")]
    public Transform pileCenterPoint;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Adds a played card (both data and physical GameObject) onto the discard pile.
    /// </summary>
    public void AddCardToPile(Card cardData, GameObject cardObj)
    {
        if (cardData != null)
        {
            discardPileData.Add(cardData);
        }

        if (cardObj != null)
        {
            discardPileObjects.Add(cardObj);

            if (pileCenterPoint != null)
            {
                // Drop the card slightly above the pile center point so gravity can pull it down naturally
                Vector3 dropSpawnPos = pileCenterPoint.position + new Vector3(
                    Random.Range(-0.02f, 0.02f),
                    0.4f + (discardPileObjects.Count * 0.02f), // Stack height offset
                    Random.Range(-0.02f, 0.02f)
                );

                cardObj.transform.position = dropSpawnPos;
                cardObj.transform.rotation = Quaternion.Euler(
                    Random.Range(90f, 90f),
                    Random.Range(-10f, 10f),
                    Random.Range(-10f, 10f)
                );

                // Enable physics so it drops satisfyingly onto the pile
                Rigidbody rb = cardObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = new Vector3(Random.Range(-0.2f, 0.2f), -0.1f, Random.Range(-0.2f, 0.2f));
                }

                Collider col = cardObj.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true;
                }
            }
        }
    }

    /// <summary>
    /// Clears the discard pile (used when a penalty is triggered or pile is burned).
    /// </summary>
    public void ClearPile()
    {
        foreach (GameObject obj in discardPileObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        discardPileObjects.Clear();
        discardPileData.Clear();
        Debug.Log("[PileManager] Discard pile cleared.");
    }

    /// <summary>
    /// Returns the full list of card data currently on the discard pile.
    /// </summary>
    public List<Card> GetDiscardPile()
    {
        return discardPileData;
    }

    /// <summary>
    /// Returns the top card data on the pile, or null if the pile is empty.
    /// </summary>
    public Card GetTopCard()
    {
        if (discardPileData.Count > 0)
        {
            return discardPileData[discardPileData.Count - 1];
        }
        return null;
    }

    /// <summary>
    /// Returns true if the discard pile has cards.
    /// </summary>
    public bool HasCards()
    {
        return discardPileData.Count > 0;
    }
}