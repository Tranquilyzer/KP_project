using System.Collections.Generic;
using UnityEngine;

// Вождение машины NPC-водителем. Добавляется на машину сам, когда в неё садится NPC.
// Обычная машина катается по дорогам (сетка Grid2D) между случайными точками.
// Полицейская машина при розыске едет к игроку, останавливается рядом, и полицейские выходят.
[RequireComponent(typeof(Car))]
public class CarAI : MonoBehaviour
{
    [Tooltip("Скорость обычной езды.")]
    public float cruiseSpeed = 7f;
    [Tooltip("Скорость полицейской машины в погоне.")]
    public float chaseSpeed = 13f;
    [Tooltip("На каком расстоянии от игрока полиция тормозит и выходит.")]
    public float policeStopDistance = 9f;
    public float repathInterval = 1f;

    Car car;
    Rigidbody2D rb;
    Collider2D[] ownColliders;
    float halfLength = 1.5f;
    float halfWidth = 0.8f;

    readonly List<Vector2> path = new List<Vector2>();
    int pathIndex;
    Vector2 destination;
    bool hasDestination;
    float nextRepath;
    float stuckTimer;
    float reverseUntil;
    float chaseStuckTime;

    WantedLevel Wanted => WantedLevel.Instance;

    void Awake()
    {
        car = GetComponent<Car>();
        rb = GetComponent<Rigidbody2D>();
        ownColliders = GetComponentsInChildren<Collider2D>();

        // Размер машины — по её коллайдерам (без триггеров), в локальных осях.
        foreach (Collider2D col in ownColliders)
        {
            if (col.isTrigger)
                continue;
            Vector3 size = col.bounds.size;
            halfLength = Mathf.Max(halfLength, Mathf.Max(size.x, size.y) * 0.5f);
            halfWidth = Mathf.Max(0.4f, Mathf.Min(size.x, size.y) * 0.5f);
        }
    }

    void Update()
    {
        if (!car.HasAiDriver)
            return;

        // Горящую машину водитель бросает и убегает.
        if (car.maxHp > 0f && car.hp <= car.maxHp * car.fireAt)
        {
            Vector2 from = transform.position;
            List<NPC> crew = new List<NPC>(car.passengers) { car.driver };
            car.UnloadAll();
            foreach (NPC npc in crew)
            {
                if (npc != null)
                    npc.Panic(from);
            }
            return;
        }

        Transform player = Wanted.PlayerTarget;
        if (car.isPolice && Wanted.level > 0 && player != null)
            ChaseUpdate(player);
        else
            TrafficUpdate();
    }

    // ---------- Режимы ----------

    void TrafficUpdate()
    {
        if (!hasDestination || Vector2.Distance(rb.position, destination) < 3f)
            PickDestination();

        DriveTo(destination, cruiseSpeed, null);
    }

    void ChaseUpdate(Transform player)
    {
        float distance = Vector2.Distance(rb.position, player.position);
        if (distance <= policeStopDistance)
        {
            car.SetAiInput(car.ForwardSpeed > 0.3f ? -1f : 0f, 0f);
            if (Mathf.Abs(car.ForwardSpeed) < 1f)
                car.UnloadAll();
            return;
        }

        // Совсем застряли недалеко от игрока — выходим пешком.
        if (Mathf.Abs(car.ForwardSpeed) < 0.5f && distance < policeStopDistance * 3f)
        {
            chaseStuckTime += Time.deltaTime;
            if (chaseStuckTime > 4f)
            {
                car.UnloadAll();
                return;
            }
        }
        else
            chaseStuckTime = 0f;

        DriveTo(player.position, chaseSpeed, player);
    }

    void PickDestination()
    {
        Grid2D grid = Grid2D.Instance;
        hasDestination = true;
        for (int i = 0; i < 20; i++)
        {
            Vector2 candidate;
            if (grid != null && grid.Grid != null)
            {
                Node2D node = grid.Grid[Random.Range(0, grid.gridSizeX), Random.Range(0, grid.gridSizeY)];
                if (!grid.IsPassable(node))
                    continue;
                candidate = node.worldPosition;
            }
            else
                candidate = rb.position + Random.insideUnitCircle * 40f;

            float distance = Vector2.Distance(rb.position, candidate);
            if (distance > 12f && distance < 60f)
            {
                destination = candidate;
                nextRepath = 0f;
                return;
            }
        }
        destination = rb.position + (Vector2)transform.up * 20f;
        nextRepath = 0f;
    }

    // ---------- Езда ----------

    void DriveTo(Vector2 target, float speed, Transform ignore)
    {
        if (Time.time >= nextRepath || (target - destination).sqrMagnitude > 16f)
            BuildPath(target);

        Grid2D grid = Grid2D.Instance;
        float reach = grid != null ? Mathf.Max(grid.CellWorldSize.x, grid.CellWorldSize.y) * 0.9f : 2f;
        while (pathIndex < path.Count - 1 && (path[pathIndex] - rb.position).sqrMagnitude < reach * reach)
            pathIndex++;

        Vector2 waypoint = path.Count > 0 ? path[pathIndex] : target;
        Steer(waypoint, speed, ignore);
    }

    void BuildPath(Vector2 target)
    {
        destination = target;
        nextRepath = Time.time + repathInterval + Random.Range(0f, 0.3f);
        path.Clear();
        pathIndex = 0;

        Grid2D grid = Grid2D.Instance;
        List<Node2D> nodes = grid != null ? Pathfinding2D.FindPath(grid, rb.position, target) : null;
        if (nodes == null)
            return;
        foreach (Node2D node in nodes)
            path.Add(node.worldPosition);
    }

    void Steer(Vector2 waypoint, float desiredSpeed, Transform ignore)
    {
        Vector2 forward = transform.up;
        Vector2 toWaypoint = waypoint - rb.position;
        // Угол > 0 — цель слева. У машины руль вправо = +1, поэтому знак меняем.
        float angle = Vector2.SignedAngle(forward, toWaypoint);
        float steerInput = Mathf.Clamp(-angle / 35f, -1f, 1f);

        float forwardSpeed = car.ForwardSpeed;
        float targetSpeed = desiredSpeed * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(Mathf.Abs(angle) / 90f));
        if (ObstacleAhead(forwardSpeed, ignore))
            targetSpeed = 0f;

        float throttleInput;
        if (forwardSpeed < targetSpeed - 0.5f)
            throttleInput = 1f;
        else if (forwardSpeed > targetSpeed + 1.5f)
            throttleInput = -1f;
        else
            throttleInput = 0f;
        if (targetSpeed <= 0f && forwardSpeed < 0.3f)
            throttleInput = 0f;

        // Хочет ехать, но стоит — упёрся. Сдаём назад с обратным рулём.
        if (targetSpeed > 1f && Mathf.Abs(forwardSpeed) < 0.5f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 2f)
            {
                stuckTimer = 0f;
                reverseUntil = Time.time + 1.2f;
                nextRepath = 0f;
            }
        }
        else
            stuckTimer = 0f;

        if (Time.time < reverseUntil)
        {
            throttleInput = -1f;
            steerInput = -steerInput;
        }

        car.SetAiInput(throttleInput, steerInput);
    }

    // Есть ли что-то прямо перед машиной (люди, машины, стены).
    bool ObstacleAhead(float forwardSpeed, Transform ignore)
    {
        Vector2 forward = transform.up;
        Vector2 origin = rb.position + forward * (halfLength + 0.2f);
        float distance = 1.5f + Mathf.Max(forwardSpeed, 0f) * 0.5f;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, halfWidth * 0.8f, forward, distance);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.isTrigger || hit.transform.IsChildOf(transform))
                continue;
            if (ignore != null && hit.transform.IsChildOf(ignore))
                continue;
            if (hit.collider.GetComponent<Bullet>() != null)
                continue;
            NPC npc = hit.collider.GetComponentInParent<NPC>();
            if (npc != null && npc.IsDead)
                continue;
            return true;
        }
        return false;
    }
}
