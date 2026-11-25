using UnityEngine;

// Пиксельный взрыв: квадратные частицы огня, дыма и искр + короткая вспышка.
// Если в машине задан свой префаб эффекта, используется он.
public class ExplosionEffect : MonoBehaviour
{
    // Размер одного "пикселя" в юнитах. 16 — как у спрайтов с Pixels Per Unit = 16.
    const float PixelsPerUnit = 16f;
    const float Pixel = 1f / PixelsPerUnit;

    public float duration = 0.25f;

    static Sprite flashSprite;
    static Material particleMaterial;

    SpriteRenderer spriteRenderer;
    float maxScale;
    float time;

    public static void Spawn(Vector2 position, float radius, GameObject prefab, float prefabLifetime = 3f)
    {
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, prefabLifetime);
            return;
        }

        GameObject root = new GameObject("PixelExplosion");
        root.transform.position = position;

        Material material = ParticleMaterial();
        CreateBurst(root.transform, material, "Smoke", 35,
            new Vector2(radius * 0.4f, radius * 1.2f), new Vector2(0.8f, 1.5f), new Vector2(5f, 9f),
            SmokeGradient(), 2.5f, 101);
        CreateBurst(root.transform, material, "Fire", 60,
            new Vector2(radius * 1f, radius * 3f), new Vector2(0.3f, 0.7f), new Vector2(3f, 6f),
            FireGradient(), 5f, 102);
        CreateBurst(root.transform, material, "Sparks", 30,
            new Vector2(radius * 2f, radius * 4f), new Vector2(0.2f, 0.5f), new Vector2(1f, 2f),
            SparkGradient(), 1.5f, 103);

        GameObject flash = new GameObject("Flash");
        flash.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = flash.AddComponent<SpriteRenderer>();
        renderer.sprite = FlashSprite();
        renderer.sortingOrder = 104;

        ExplosionEffect effect = flash.AddComponent<ExplosionEffect>();
        effect.spriteRenderer = renderer;
        effect.maxScale = radius * 1.4f;
        effect.UpdateFlash(0f);

        Destroy(root, 2f);
    }

    void Update()
    {
        time += Time.deltaTime;
        float k = time / duration;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        UpdateFlash(k);
    }

    void UpdateFlash(float k)
    {
        // Масштаб меняется ступеньками, чтобы вспышка не выглядела гладкой.
        float steps = 4f;
        float stepped = Mathf.Ceil(Mathf.Max(k, 0.01f) * steps) / steps;
        transform.localScale = Vector3.one * Mathf.Lerp(maxScale * 0.4f, maxScale, stepped);
        spriteRenderer.color = k < 0.5f ? new Color(1f, 1f, 0.85f, 1f) : new Color(1f, 0.7f, 0.2f, 0.8f);
    }

    // Постоянный дым (например, у подбитой машины). Включается/выключается через Play/Stop.
    public static ParticleSystem CreateSmokeEmitter(Transform parent, Vector3 localPosition)
    {
        return CreateLoop(parent, localPosition, "Smoke", 14f,
            new Vector2(0.4f, 1.2f), new Vector2(1f, 2f), new Vector2(4f, 7f),
            SmokeGradient(), AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f), 60);
    }

    // Постоянный огонь.
    public static ParticleSystem CreateFireEmitter(Transform parent, Vector3 localPosition)
    {
        return CreateLoop(parent, localPosition, "Fire", 30f,
            new Vector2(0.5f, 1.8f), new Vector2(0.25f, 0.6f), new Vector2(3f, 6f),
            FireGradient(), AnimationCurve.Linear(0f, 1f, 1f, 0.3f), 61);
    }

    static ParticleSystem CreateLoop(Transform parent, Vector3 localPosition, string name, float rate,
        Vector2 speed, Vector2 lifetime, Vector2 sizeInPixels, Gradient colors, AnimationCurve sizeCurve, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeInPixels.x * Pixel, sizeInPixels.y * Pixel);
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        // Частицы остаются там, где появились, — за едущей машиной тянется шлейф.
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        shape.radiusThickness = 1f;
        shape.rotation = Vector3.zero;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = colors;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = sortingOrder;
        Material material = ParticleMaterial();
        if (material != null)
            renderer.sharedMaterial = material;

        return ps;
    }

    static void CreateBurst(Transform parent, Material material, string name, int count,
        Vector2 speed, Vector2 lifetime, Vector2 sizeInPixels, Gradient colors, float drag, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeInPixels.x * Pixel, sizeInPixels.y * Pixel);
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        shape.radiusThickness = 1f;
        shape.rotation = Vector3.zero;

        ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1000f;
        limit.drag = drag;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = colors;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        ParticleSystemRenderer renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = sortingOrder;
        if (material != null)
            renderer.sharedMaterial = material;

        ps.Play();
    }

    // Fixed-градиенты переключают цвет резко, без плавного перехода — как в пиксель-арте.
    static Gradient FireGradient()
    {
        return FixedGradient(
            new[] { new Color(1f, 1f, 0.9f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.55f, 0.1f), new Color(0.85f, 0.2f, 0.05f), new Color(0.35f, 0.1f, 0.05f) },
            new[] { 0.1f, 0.3f, 0.55f, 0.8f, 1f },
            0.85f);
    }

    static Gradient SmokeGradient()
    {
        return FixedGradient(
            new[] { new Color(0.45f, 0.4f, 0.38f), new Color(0.3f, 0.28f, 0.27f), new Color(0.18f, 0.17f, 0.17f) },
            new[] { 0.3f, 0.65f, 1f },
            0.8f);
    }

    static Gradient SparkGradient()
    {
        return FixedGradient(
            new[] { Color.white, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.5f, 0.1f) },
            new[] { 0.3f, 0.7f, 1f },
            0.9f);
    }

    static Gradient FixedGradient(Color[] colors, float[] times, float fadeAt)
    {
        GradientColorKey[] colorKeys = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            colorKeys[i] = new GradientColorKey(colors[i], times[i]);

        Gradient gradient = new Gradient();
        gradient.mode = GradientMode.Fixed;
        gradient.SetKeys(colorKeys, new[]
        {
            new GradientAlphaKey(1f, fadeAt),
            new GradientAlphaKey(0f, 1f)
        });
        return gradient;
    }

    static Material ParticleMaterial()
    {
        if (particleMaterial != null)
            return particleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();

        particleMaterial = new Material(shader);
        particleMaterial.mainTexture = texture;
        return particleMaterial;
    }

    // Пиксельный круг 12x12 с жёсткими краями.
    static Sprite FlashSprite()
    {
        if (flashSprite != null)
            return flashSprite;

        const int size = 12;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, distance <= center + 0.2f ? Color.white : Color.clear);
            }
        }
        texture.Apply();

        flashSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return flashSprite;
    }
}
