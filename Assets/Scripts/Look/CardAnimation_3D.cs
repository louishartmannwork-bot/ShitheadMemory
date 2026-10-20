using UnityEngine;
using System;
using System.Collections;

public class CardAnimation_3D : MonoBehaviour
{
    private Coroutine activeAnimation;

    // Sanftes Nachverfolgen von Zielposition, Zielrotation UND Zielskalierung
    public void MoveTowards(Vector3 targetPos, Quaternion targetRot, Vector3 targetScale, float speed)
    {
        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * speed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Time.deltaTime * speed);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * speed);
    }

    public void PlayTween(Vector3 targetPos, Quaternion targetRot, float duration, Action onComplete = null)
    {
        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }
        activeAnimation = StartCoroutine(TweenRoutine(targetPos, targetRot, duration, onComplete));
    }

    private IEnumerator TweenRoutine(Vector3 targetPos, Quaternion targetRot, float duration, Action onComplete)
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, t);

            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
        activeAnimation = null;

        onComplete?.Invoke();
    }
}