using Unity.Cinemachine;
using UnityEngine;

// Тряска камеры. Сам добавляется на CinemachineCamera при первом вызове Shake.
public class CameraShake : CinemachineExtension
{
    public float maxDistance = 30f;
    public float frequency = 25f;

    static CameraShake instance;

    float strength;
    float duration;
    float timeLeft;
    float seed;

    public static void Shake(Vector2 source, float power, float time)
    {
        if (instance == null)
        {
            CinemachineCamera cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCamera == null)
                return;

            instance = cinemachineCamera.GetComponent<CameraShake>();
            if (instance == null)
                instance = cinemachineCamera.gameObject.AddComponent<CameraShake>();
        }

        instance.AddShake(source, power, time);
    }

    void AddShake(Vector2 source, float power, float time)
    {
        Camera mainCamera = Camera.main;
        float distance = mainCamera != null ? Vector2.Distance(mainCamera.transform.position, source) : 0f;
        float scaled = power * (1f - Mathf.Clamp01(distance / Mathf.Max(maxDistance, 0.01f)));
        if (scaled <= 0f || time <= 0f)
            return;

        float current = timeLeft > 0f ? strength * timeLeft / duration : 0f;
        if (scaled < current)
            return;

        strength = scaled;
        duration = time;
        timeLeft = time;
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (timeLeft > 0f)
            timeLeft -= Time.deltaTime;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || timeLeft <= 0f || duration <= 0f)
            return;

        float fade = timeLeft / duration;
        float amount = strength * fade * fade;
        float t = Time.time * frequency;
        Vector2 offset = new Vector2(
            Mathf.PerlinNoise(seed, t) - 0.5f,
            Mathf.PerlinNoise(seed + 50f, t) - 0.5f) * 2f * amount;

        state.PositionCorrection += (Vector3)offset;
    }
}
