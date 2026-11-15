using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class EnemyHandManager_3D : MonoBehaviour
{
    [Header("References")]
    public DeckManager_3D deckManager;
    public GameObject cardPrefab;

    [Header("Enemy Hand Layout Settings")]
    public float widthSpread = 0.5f;
    public float maxHandWidth = 4.0f;
    public float baseHeightY = 0.8f;
    public float baseZPos = 2.5f;
    public float layerOffsetY = 0.015f;
    public float smoothSpeed = 12f;

    public List<GameObject> enemyHandCards = new List<GameObject>();
    private List<Card> enemyCardDatas = new List<Card>();
    private Vector3 defaultCardScale = Vector3.one;

    private void Start()
    {
        if (cardPrefab != null)
        {
            defaultCardScale = cardPrefab.transform.localScale;
        }
    }

    private void Update()
    {
        int totalCards = enemyHandCards.Count;
        if (totalCards == 0) return;

        float currentSpread = widthSpread;
        if (totalCards > 1)
        {
            float totalCalculatedWidth = (totalCards - 1) * widthSpread;
            if (totalCalculatedWidth > maxHandWidth)
            {
                currentSpread = maxHandWidth / (totalCards - 1);
            }
        }

        for (int i = 0; i < totalCards; i++)
        {
            GameObject card = enemyHandCards[i];
            if (card == null) continue;

            float xPos = (i - (totalCards - 1) / 2.0f) * currentSpread;
            float yPos = baseHeightY + (i * layerOffsetY);
            float zPos = baseZPos;

            Vector3 targetPos = new Vector3(xPos, yPos, zPos);
            Quaternion targetRot = Quaternion.Euler(75f, 180f, xPos * 10f);

            CardAnimation_3D anim = card.GetComponent<CardAnimation_3D>();
            if (anim != null)
            {
                anim.MoveTowards(targetPos, targetRot, defaultCardScale, smoothSpeed);
            }
            else
            {
                card.transform.position = targetPos;
                card.transform.rotation = targetRot;
            }
        }
    }

    public void AddCardVisual(Card cardData)
    {
        if (cardPrefab == null) return;

        Vector3 spawnPos = new Vector3(0f, 1.0f, 2.5f);
        GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 180f, 0f));

        Card3DViewTest cardView = cardObj.GetComponent<Card3DViewTest>();
        cardView?.SetupCard(cardData);

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable != null) Destroy(draggable);

        Collider col = cardObj.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        enemyHandCards.Add(cardObj);
        enemyCardDatas.Add(cardData);
    }

    public void AddExistingCardVisual(GameObject cardObj, Card cardData)
    {
        if (cardObj == null) return;

        Card3DViewTest cardView = cardObj.GetComponent<Card3DViewTest>();
        cardView?.SetupCard(cardData);

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable != null) Destroy(draggable);

        Collider col = cardObj.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Rigidbody rb = cardObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (!enemyHandCards.Contains(cardObj))
        {
            enemyHandCards.Add(cardObj);
            enemyCardDatas.Add(cardData);
        }
    }

    public void RemoveCardVisual(int index)
    {
        if (index >= 0 && index < enemyHandCards.Count)
        {
            // ONLY unreference from lists so the card object can safely tween to the pile.
            // DO NOT destroy the GameObject here!
            enemyHandCards.RemoveAt(index);

            if (index < enemyCardDatas.Count)
            {
                enemyCardDatas.RemoveAt(index);
            }
        }
    }

    public void ClearAllVisuals()
    {
        foreach (GameObject cardObj in enemyHandCards)
        {
            if (cardObj != null) Destroy(cardObj);
        }
        enemyHandCards.Clear();
        enemyCardDatas.Clear();
    }

    public List<Card> GetEnemyHandCardData()
    {
        // SELF-HEALING SANITY CHECK: Prune any null visual references automatically
        for (int i = enemyHandCards.Count - 1; i >= 0; i--)
        {
            if (enemyHandCards[i] == null)
            {
                enemyHandCards.RemoveAt(i);
                if (i < enemyCardDatas.Count) enemyCardDatas.RemoveAt(i);
            }
        }

        // Hard sync safeguard: Truncate trailing ghost data entries if counts mismatch
        if (enemyCardDatas.Count != enemyHandCards.Count)
        {
            Debug.LogWarning($"[EnemyHandManager] Desync detected! Visuals: {enemyHandCards.Count}, Data: {enemyCardDatas.Count}. Auto-syncing...");
            while (enemyCardDatas.Count > enemyHandCards.Count)
            {
                enemyCardDatas.RemoveAt(enemyCardDatas.Count - 1);
            }
        }

        return new List<Card>(enemyCardDatas);
    }
}