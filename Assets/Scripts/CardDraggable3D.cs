using UnityEngine;

public class CardDraggable3D : MonoBehaviour
{
    private Camera cam;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 lastMousePos;
    private Collider cardCollider;
    private bool isFlippedToFront = false;

    void Start()
    {
        cam = Camera.main;
        dragPlane = new Plane(Vector3.up, new Vector3(0, 0.6f, 0));
        cardCollider = GetComponent<Collider>();
    }

    // Wird vom DeckManager aufgerufen, wenn die Karte direkt vom Stapel gezogen wird
    public void StartDraggingImmediately()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        isDragging = true;
        isFlippedToFront = false; // Startet verdeckt

        lastMousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Vector3 pos = transform.position;
        pos.y = 0.6f;
        transform.position = pos;
    }

    void OnMouseDown()
    {
        // Normales Anklicken einer bereits liegenden Karte
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        isDragging = true;
        isFlippedToFront = true; // Liegt sie schon auf dem Tisch, ist sie aufgedeckt

        lastMousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Vector3 pos = transform.position;
        pos.y = 0.6f;
        transform.position = pos;
    }

    void Update()
    {
        if (!isDragging) return;

        Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Vector3 mouseDelta = (Vector3)mouseScreenPos - lastMousePos;
        lastMousePos = mouseScreenPos;

        Ray ray = cam.ScreenPointToRay(mouseScreenPos);
        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 targetPoint = ray.GetPoint(enter);
            targetPoint.y = 0.6f;
            transform.position = targetPoint;
        }

        // Prüfen, ob die Karte über der Hand-Area oder dem Ablagestapel ist zum Aufdecken ("Flippen")
        RaycastHit hitCheck;
        Ray downRay = new Ray(new Vector3(transform.position.x, 5f, transform.position.z), Vector3.down);
        if (Physics.Raycast(downRay, out hitCheck, 10f))
        {
            if (hitCheck.collider.gameObject.name.Contains("HandArea") || hitCheck.collider.gameObject.name == "PileArea_3D")
            {
                isFlippedToFront = true; // Aufdecken sobald über Hand oder Ablagestapel
            }
        }

        float tiltZ = Mathf.Clamp(-mouseDelta.x * 0.1f, -15f, 15f);

        if (isFlippedToFront)
        {
            transform.rotation = Quaternion.Euler(75f, transform.eulerAngles.y, tiltZ);
        }
        else
        {
            transform.rotation = Quaternion.Euler(-75f, transform.eulerAngles.y + 180f, tiltZ);
        }

        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
        {
            DropCard();
        }
    }

    void DropCard()
    {
        isDragging = false;

        if (cardCollider != null) cardCollider.enabled = false;

        Vector3 rayStartPos = new Vector3(transform.position.x, 5f, transform.position.z);
        Ray ray = new Ray(rayStartPos, Vector3.down);

        RaycastHit hit;
        bool isValidDrop = false;

        if (Physics.Raycast(ray, out hit, 10f))
        {
            string hitObjectName = hit.collider.gameObject.name;
            Debug.Log($"Raycast hat getroffen: {hitObjectName}");

            if (hitObjectName == "PileArea_3D" || hitObjectName.Contains("HandArea"))
            {
                isValidDrop = true;

                if (hitObjectName == "PileArea_3D")
                {
                    // Auf dem Ablagestapel flach hinlegen mit leichter Drehung
                    transform.position = new Vector3(hit.point.x, 0.16f, hit.point.z);
                    float randomTilt = Random.Range(-10f, 10f);
                    transform.rotation = Quaternion.Euler(90f, 0f, randomTilt);

                    Destroy(this); // Drag-Skript entfernen, Karte liegt fest
                }
                else
                {
                    // IN DER HAND-AREA: Bleibt exakt da liegen, wo du sie losgelassen hast!
                    transform.position = new Vector3(transform.position.x, 0.35f, transform.position.z);

                    // Dreht sich in die typische Hand-Neigung (z.B. 60 Grad angewinkelt zur Kamera)
                    transform.rotation = Quaternion.Euler(60f, 0f, 0f);

                    if (cardCollider != null) cardCollider.enabled = true;

                    // Wir erlauben, dass man sie danach wieder greifen kann (Skript bleibt aktiv!)
                    Debug.Log("Karte in der Hand-Area frei abgelegt.");
                    return;
                }

                Debug.Log($"Karte erfolgreich auf {hitObjectName} platziert!");
                return;
            }
        }

        // Wenn im Niemandsland losgelassen: Zurück an den Ursprung
        if (!isValidDrop)
        {
            if (cardCollider != null) cardCollider.enabled = true;

            transform.position = originalPosition;
            transform.rotation = originalRotation;
            Debug.Log("Ungültiger Ablageort – Karte kehrt zurück.");
        }
    }
}