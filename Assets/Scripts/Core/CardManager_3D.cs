using UnityEngine;
using ShitheadMemory;
using System;
using System.Collections;

public class CardManager_3D : MonoBehaviour
{
    public static CardManager_3D Instance;

    [Header("Global Prefabs & Settings")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float cardThickness = 0.002f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    #region Card Spawning & Factory Methods
    /// <summary>
    /// Instantiates and initializes a physical card object with data and components.
    /// </summary>
    public GameObject SpawnCard(Vector3 position, Quaternion rotation, Card cardData, Transform parent = null, bool faceDown = false)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("[CardManager] Card Prefab is missing!");
            return null;
        }

        GameObject cardObj = Instantiate(cardPrefab, position, rotation, parent);

        // Setup card visual representation
        cardObj.GetComponent<Card3DViewTest>()?.SetupCard(cardData);

        // Ensure Draggable component exists and is initialized
        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable == null)
        {
            draggable = cardObj.AddComponent<CardDraggable3D>();
        }
        draggable.cardData = cardData;
        draggable.SetFaceDown(faceDown);

        // Ensure Rigidbody & Collider setup
        ConfigurePhysics(cardObj, false);

        return cardObj;
    }

    /// <summary>
    /// Configures rigidbody and collider states for physics control.
    /// </summary>
    public void ConfigurePhysics(GameObject cardObj, bool enableGravity)
    {
        Collider col = cardObj.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Rigidbody rb = cardObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = !enableGravity;
            rb.useGravity = enableGravity;
        }
    }
    #endregion

    #region Animation & Tween Helpers
    /// <summary>
    /// Smoothly tweens a card to a target position and rotation, executing an optional callback upon completion.
    /// </summary>
    public void TweenCardTo(GameObject cardObj, Vector3 targetPos, Quaternion targetRot, float duration, Action onComplete = null)
    {
        if (cardObj == null) return;
        StartCoroutine(TweenRoutine(cardObj, targetPos, targetRot, duration, onComplete));
    }

    private IEnumerator TweenRoutine(GameObject cardObj, Vector3 targetPos, Quaternion targetRot, float duration, Action onComplete)
    {
        Vector3 startPos = cardObj.transform.position;
        Quaternion startRot = cardObj.transform.rotation;
        float elapsed = 0f;

        // Disable physics during custom tweens
        ConfigurePhysics(cardObj, false);

        while (elapsed < duration)
        {
            if (cardObj == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);

            cardObj.transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            cardObj.transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

            yield return null;
        }

        if (cardObj != null)
        {
            cardObj.transform.position = targetPos;
            cardObj.transform.rotation = targetRot;
            ConfigurePhysics(cardObj, true); // Re-enable physics/interaction readiness
        }

        onComplete?.Invoke();
    }
    #endregion
}