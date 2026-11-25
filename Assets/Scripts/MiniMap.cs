using UnityEngine;
using UnityEngine.UI;

// Круглая мини-карта. Вешается на Raw Image в Canvas.
// Сама создаёт вторую камеру над игроком, которая рисует мир в текстуру,
// обрезает картинку кругом, добавляет рамку и стрелку игрока в центре.
[RequireComponent(typeof(RawImage))]
public class MiniMap : MonoBehaviour
{
    [Header("Камера карты")]
    [Tooltip("Сколько мира видно на карте: половина высоты в юнитах. Больше — карта мельче.")]
    public float viewSize = 30f;
    [Tooltip("Разрешение текстуры карты. Меньше — пиксельнее и быстрее.")]
    public int resolution = 256;
    [Tooltip("Какие слои рисовать на карте.")]
    public LayerMask mapLayers = ~0;
    public Color backgroundColor = new Color(0.08f, 0.08f, 0.1f);
    [Tooltip("Вкл — карта поворачивается вместе с игроком (верх карты = куда он смотрит).")]
    public bool rotateWithPlayer = false;

    [Header("Вид")]
    public float borderWidth = 4f;
    public Color borderColor = new Color(0.05f, 0.05f, 0.05f);
    public float playerMarkerSize = 18f;
    public Color playerColor = new Color(0.2f, 1f, 0.3f);

    RawImage image;
    RectTransform rect;
    Camera mapCamera;
    RenderTexture texture;
    Image playerMarker;

    static Sprite circleSprite;
    static Sprite arrowSprite;

    void Start()
    {
        image = GetComponent<RawImage>();
        rect = (RectTransform)transform;

        MakeRound();
        CreateCamera();
        playerMarker = CreatePlayerMarker();
    }

    void OnDestroy()
    {
        if (texture != null)
            texture.Release();
        if (mapCamera != null)
            Destroy(mapCamera.gameObject);
    }

    // Кладём карту внутрь круглой маски, а позади — круг-рамку чуть больше.
    void MakeRound()
    {
        Transform parent = rect.parent;
        int index = rect.GetSiblingIndex();

        RectTransform border = CreateUiObject("MiniMapBorder", parent);
        CopyLayout(rect, border, borderWidth);
        Image borderImage = border.gameObject.AddComponent<Image>();
        borderImage.sprite = CircleSprite();
        borderImage.color = borderColor;
        borderImage.raycastTarget = false;
        border.SetSiblingIndex(index);

        RectTransform mask = CreateUiObject("MiniMapMask", parent);
        CopyLayout(rect, mask, 0f);
        Image maskImage = mask.gameObject.AddComponent<Image>();
        maskImage.sprite = CircleSprite();
        maskImage.raycastTarget = false;
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        mask.SetSiblingIndex(index + 1);

        rect.SetParent(mask, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image.raycastTarget = false;
    }

    static RectTransform CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent != null ? parent.gameObject.layer : obj.layer;
        RectTransform result = (RectTransform)obj.transform;
        result.SetParent(parent, false);
        return result;
    }

    static void CopyLayout(RectTransform from, RectTransform to, float grow)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta + Vector2.one * grow * 2f;
        // Рамка больше на grow с каждой стороны — сдвигаем, чтобы центр совпал.
        Vector2 pivotShift = new Vector2((to.pivot.x - 0.5f) * grow * 2f, (to.pivot.y - 0.5f) * grow * 2f);
        to.anchoredPosition = from.anchoredPosition + pivotShift;
        to.localRotation = from.localRotation;
        to.localScale = from.localScale;
    }

    void CreateCamera()
    {
        float aspect = rect.rect.height > 0f ? rect.rect.width / rect.rect.height : 1f;
        texture = new RenderTexture(Mathf.Max(16, Mathf.RoundToInt(resolution * aspect)), resolution, 16);
        texture.filterMode = FilterMode.Point;
        texture.name = "MiniMapTexture";

        GameObject cameraObject = new GameObject("MiniMapCamera");
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = viewSize;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = backgroundColor;
        mapCamera.nearClipPlane = 0.1f;
        mapCamera.farClipPlane = 1000f;
        mapCamera.depth = -10;
        mapCamera.targetTexture = texture;

        int uiLayer = LayerMask.NameToLayer("UI");
        mapCamera.cullingMask = uiLayer >= 0 ? mapLayers & ~(1 << uiLayer) : mapLayers;

        image.texture = texture;
    }

    Image CreatePlayerMarker()
    {
        RectTransform markerRect = CreateUiObject("PlayerMarker", transform);
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(playerMarkerSize, playerMarkerSize);
        markerRect.anchoredPosition = Vector2.zero;

        Image marker = markerRect.gameObject.AddComponent<Image>();
        marker.sprite = ArrowSprite();
        marker.color = playerColor;
        marker.raycastTarget = false;
        return marker;
    }

    void LateUpdate()
    {
        Transform target = WantedLevel.Instance.PlayerTarget;
        if (target == null || mapCamera == null)
            return;

        mapCamera.orthographicSize = viewSize;
        Vector3 position = target.position;
        mapCamera.transform.position = new Vector3(position.x, position.y, position.z - 50f);

        // Стрелка нарисована «вверх». У пешего игрока перед — transform.right, у машины — transform.up.
        float heading = target.eulerAngles.z - (target.GetComponent<Car>() != null ? 0f : 90f);
        if (rotateWithPlayer)
        {
            mapCamera.transform.rotation = Quaternion.Euler(0f, 0f, heading);
            playerMarker.rectTransform.localRotation = Quaternion.identity;
        }
        else
        {
            mapCamera.transform.rotation = Quaternion.identity;
            playerMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, heading);
        }
    }

    static Sprite CircleSprite()
    {
        if (circleSprite == null)
        {
            circleSprite = MakeSprite(128, FilterMode.Bilinear, (x, y, s) =>
            {
                float c = (s - 1) * 0.5f;
                return Mathf.Clamp01(c + 0.5f - Vector2.Distance(new Vector2(x, y), new Vector2(c, c)));
            });
        }
        return circleSprite;
    }

    // Треугольник остриём вверх.
    static Sprite ArrowSprite()
    {
        if (arrowSprite == null)
            arrowSprite = MakeSprite(16, FilterMode.Point, (x, y, s) => Mathf.Abs(x - (s - 1) * 0.5f) <= (s - 1 - y) * 0.5f ? 1f : 0f);
        return arrowSprite;
    }

    static Sprite MakeSprite(int size, FilterMode filter, System.Func<int, int, int, float> alpha)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = filter;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y, size)));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
