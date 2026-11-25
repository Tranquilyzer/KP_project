using UnityEngine;

// Проигрывание коротких звуков. Чем дальше источник от камеры, тем тише звук.
public static class Sound
{
    public static float maxDistance = 25f;

    // Возвращает длительность звучания в секундах (с учётом высоты тона), 0 — если звука нет.
    public static float Play(AudioClip clip, Vector2 position, float volume = 1f, float pitchVariation = 0.08f)
    {
        if (clip == null)
            return 0f;

        float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        float duration = clip.length / Mathf.Max(pitch, 0.1f);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float distance = Vector2.Distance(mainCamera.transform.position, position);
            volume *= 1f - Mathf.Clamp01(distance / maxDistance);
        }
        if (volume <= 0.01f)
            return duration;

        GameObject obj = new GameObject("Sound_" + clip.name);
        obj.transform.position = position;

        AudioSource source = obj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.spatialBlend = 0f;
        source.Play();

        Object.Destroy(obj, duration + 0.1f);
        return duration;
    }
}
