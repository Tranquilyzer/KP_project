using System.Collections.Generic;
using UnityEngine;

// Досоздаёт NPC, пока живых на сцене меньше maxNpc.
// Появляются они вне экрана и не ближе minDistanceFromPlayer к игроку.
public class NpcSpawner : MonoBehaviour
{
  
    public GameObject[] npcPrefabs;
  
    public Transform[] spawnPoints;

    public int maxNpc = 20;

    public float spawnInterval = 2f;
    public float minDistanceFromPlayer = 12f;

    public float clearRadius = 0.6f;

    public float corpseLifetime = 30f;

    float nextSpawnTime;

    void Update()
    {
        if (Time.time < nextSpawnTime)
            return;
        nextSpawnTime = Time.time + spawnInterval;

        NPC[] all = FindObjectsByType<NPC>(FindObjectsSortMode.None);
        int alive = 0;
        foreach (NPC npc in all)
        {
            if (!npc.IsDead)
                alive++;
            else if (corpseLifetime > 0f && Time.time - npc.DeathTime > corpseLifetime && !IsOnScreen(npc.transform.position))
                Destroy(npc.gameObject);
        }

        if (alive < maxNpc)
            TrySpawn();
    }

    void TrySpawn()
    {
        GameObject prefab = RandomPrefab();
        if (prefab == null)
            return;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (!TryGetCandidate(out Vector2 position))
                return;
            if (!IsGoodPlace(position))
                continue;

            Instantiate(prefab, position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            return;
        }
    }

    GameObject RandomPrefab()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0)
            return null;

        List<GameObject> valid = new List<GameObject>();
        foreach (GameObject prefab in npcPrefabs)
        {
            if (prefab != null)
                valid.Add(prefab);
        }
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    bool TryGetCandidate(out Vector2 position)
    {
        position = Vector2.zero;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null)
                return false;
            position = point.position;
            return true;
        }

        Grid2D grid = Grid2D.Instance;
        if (grid == null || grid.Grid == null)
            return false;

        Node2D node = grid.Grid[Random.Range(0, grid.gridSizeX), Random.Range(0, grid.gridSizeY)];
        if (!grid.IsPassable(node))
            return false;
        position = node.worldPosition;
        return true;
    }

    bool IsGoodPlace(Vector2 position)
    {
        Transform player = WantedLevel.Instance.PlayerTarget;
        if (player != null && Vector2.Distance(player.position, position) < minDistanceFromPlayer)
            return false;
        if (IsOnScreen(position))
            return false;

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, clearRadius))
        {
            if (!hit.isTrigger)
                return false;
        }
        return true;
    }

    static bool IsOnScreen(Vector2 position)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 viewport = cam.WorldToViewportPoint(position);
        return viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
    }

    void OnDrawGizmos()
    {
        if (spawnPoints == null)
            return;

        Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.9f);
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.5f);
        }
    }
}
