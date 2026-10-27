using UnityEngine;
using ShitheadMemory;

public class CardAtlasView_3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer cardRenderer; // Der Renderer der Karten-Vorderseite (oder das Objekt mit dem Material)

    [Header("Atlas Settings")]
    private const int COLUMNS = 13; // 13 Werte (2 bis Ass)
    private const int ROWS = 4;    // 4 Farben (Suits)

    [Header("Scan Puffer / Padding (0.0 bis 0.1)")]
    [Tooltip("Zieht den UV-Ausschnitt leicht nach innen, um unsaubere Ränder des Scans wegzuschneiden.")]
    [Range(0f, 0.05f)]
    public float uvPadding = 0.015f; // Standard-Puffer (kannst du im Inspector anpassen)

    public void SetupCardFromAtlas(Card cardData)
    {
        if (cardRenderer == null) cardRenderer = GetComponent<MeshRenderer>();
        if (cardRenderer == null) return;

        int xIndex = GetValueIndex(cardData.value);
        int yIndex = GetSuitIndex(cardData.suit);

        // 1. Standard-Größe einer Kachel im Atlas (z.B. 1/13 und 1/4)
        float tileWidth = 1f / COLUMNS;
        float tileHeight = 1f / ROWS;

        // 2. Skalierung mit Puffer verkleinern (damit schneiden wir den äußeren Rand der Kachel ab)
        Vector2 scale = new Vector2(tileWidth - (uvPadding * 2f), tileHeight - (uvPadding * 2f));

        // 3. Offset berechnen: Gehe zur Kachel und verschiebe den Startpunkt um den Puffer nach innen/rechts/oben
        float offsetX = (xIndex * tileWidth) + uvPadding;
        float offsetY = (yIndex * tileHeight) + uvPadding;
        Vector2 offset = new Vector2(offsetX, offsetY);

        // 4. Werte auf das Material anwenden
        Material mat = cardRenderer.material; // Erzeugt eine Instanz für diese spezifische Karte
        mat.mainTextureScale = scale;
        mat.mainTextureOffset = offset;
    }

    private int GetValueIndex(CardValue value)
    {
        // Angenommen, dein CardValue Enum beginnt bei 2 (oder passe es an deine Enum-Reihenfolge an)
        return (int)value - 2;
    }

    private int GetSuitIndex(CardSuit suit)
    {
        return (int)suit;
    }
}