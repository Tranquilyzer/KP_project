using System;
using UnityEngine;

// Шум и опасность: выстрелы, смерти. NPC подписываются и реагируют, если находятся в радиусе.
public static class Danger
{
    public static event Action<Vector2, float, GameObject> Reported;

    public static void Report(Vector2 position, float radius, GameObject source)
    {
        Reported?.Invoke(position, radius, source);
    }
}
