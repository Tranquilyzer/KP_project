using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Сетка для поиска пути. Узлы совпадают с клетками тайлмапа, поэтому учитывается
// любой размер и масштаб Grid (например, клетки 2.5 x 3).
public class Grid2D : MonoBehaviour
{
    [Tooltip("Тайлмап, по клеткам которого можно ходить (дороги, тротуары). Пустые клетки непроходимы.")]
    public Tilemap walkableMap;
    [Tooltip("Тайлмап стен: клетки с тайлами непроходимы. Можно оставить пустым.")]
    public Tilemap obstaclemap;
    [Tooltip("Считать препятствием статичные коллайдеры в клетке (дома, заборы). Машины, NPC и игрок не учитываются.")]
    public bool useColliders = true;
    public LayerMask obstacleLayers = ~0;
    [Tooltip("Какая часть клетки проверяется на коллайдеры.")]
    [Range(0.1f, 1f)] public float colliderCheckSize = 0.6f;
    public bool allowDiagonals = true;

    [Header("Подвижные препятствия")]
    [Tooltip("Обновлять клетки, занятые машинами и другими физическими объектами.")]
    public bool trackDynamicObstacles = true;
    public float dynamicRefreshInterval = 0.5f;
    [Tooltip("Объекты быстрее этой скорости не считаются препятствием (едущие машины).")]
    public float dynamicMaxSpeed = 1.5f;

    public Node2D[,] Grid;
    public List<Node2D> path;
    public int gridSizeX, gridSizeY;

    static Grid2D instance;

    Tilemap layout;
    BoundsInt bounds;
    Vector2 cellWorldSize = Vector2.one;
    float nextDynamicRefresh;
    readonly List<Node2D> dynamicBlocked = new List<Node2D>();

    public static Grid2D Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<Grid2D>();
            return instance;
        }
    }

    public Vector2 CellWorldSize => cellWorldSize;

    void Awake()
    {
        instance = this;
        CreateGrid();
    }

    void Update()
    {
        if (!trackDynamicObstacles || Grid == null || Time.time < nextDynamicRefresh)
            return;

        nextDynamicRefresh = Time.time + dynamicRefreshInterval;
        RefreshDynamicObstacles();
    }

    // Помечает клетки, которые занимают стоящие машины и другие подвижные объекты.
    void RefreshDynamicObstacles()
    {
        foreach (Node2D node in dynamicBlocked)
            node.dynamicObstacle = false;
        dynamicBlocked.Clear();

        Vector2 min = Grid[0, 0].worldPosition;
        Vector2 max = Grid[gridSizeX - 1, gridSizeY - 1].worldPosition;
        Vector2 size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) + cellWorldSize;
        Collider2D[] hits = Physics2D.OverlapBoxAll((min + max) * 0.5f, size, 0f, obstacleLayers);

        Vector2 half = cellWorldSize * colliderCheckSize * 0.5f;
        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger)
                continue;
            Rigidbody2D body = hit.attachedRigidbody;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated)
                continue;
            if (body.linearVelocity.magnitude > dynamicMaxSpeed || IsAgent(body))
                continue;

            Bounds b = hit.bounds;
            Node2D from = NodeFromWorldPoint(b.min);
            Node2D to = NodeFromWorldPoint(b.max);
            for (int x = Mathf.Min(from.GridX, to.GridX); x <= Mathf.Max(from.GridX, to.GridX); x++)
            {
                for (int y = Mathf.Min(from.GridY, to.GridY); y <= Mathf.Max(from.GridY, to.GridY); y++)
                {
                    Node2D node = Grid[x, y];
                    Vector3 p = node.worldPosition;
                    bool overlaps = b.min.x < p.x + half.x && b.max.x > p.x - half.x &&
                                    b.min.y < p.y + half.y && b.max.y > p.y - half.y;
                    if (overlaps && !node.dynamicObstacle)
                    {
                        node.dynamicObstacle = true;
                        dynamicBlocked.Add(node);
                    }
                }
            }
        }
    }

    // Те, кто сам ходит или летит, препятствием не считаются.
    static bool IsAgent(Rigidbody2D body)
    {
        return body.GetComponent<NPC>() != null || body.GetComponent<PlayerController>() != null ||
               body.GetComponent<Bullet>() != null || body.GetComponent<ItemPickUp>() != null;
    }

    public void CreateGrid()
    {
        layout = walkableMap != null ? walkableMap : obstaclemap;
        if (layout == null)
        {
            Debug.LogWarning("Grid2D: не задан ни Walkable Map, ни Obstaclemap — поиск пути отключён.", this);
            Grid = null;
            return;
        }

        layout.CompressBounds();
        bounds = layout.cellBounds;
        gridSizeX = bounds.size.x;
        gridSizeY = bounds.size.y;

        Vector3 scale = layout.transform.lossyScale;
        Vector3 cellSize = layout.layoutGrid.cellSize;
        cellWorldSize = new Vector2(Mathf.Abs(cellSize.x * scale.x), Mathf.Abs(cellSize.y * scale.y));

        Grid = new Node2D[gridSizeX, gridSizeY];
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3Int cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                Vector3 worldPoint = layout.GetCellCenterWorld(cell);
                Grid[x, y] = new Node2D(IsBlocked(worldPoint), worldPoint, x, y);
            }
        }
    }

    bool IsBlocked(Vector3 worldPoint)
    {
        if (walkableMap != null && !walkableMap.HasTile(walkableMap.WorldToCell(worldPoint)))
            return true;
        if (obstaclemap != null && obstaclemap.HasTile(obstaclemap.WorldToCell(worldPoint)))
            return true;
        if (!useColliders)
            return false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPoint, cellWorldSize * colliderCheckSize, 0f, obstacleLayers);
        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger)
                continue;
            Rigidbody2D body = hit.attachedRigidbody;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
                continue;
            if (walkableMap != null && hit.transform.IsChildOf(walkableMap.transform))
                continue;
            return true;
        }
        return false;
    }

    // Можно ли пройти через узел.
    public bool IsPassable(Node2D node)
    {
        return node != null && !node.obstacle && !node.dynamicObstacle;
    }

    public List<Node2D> GetNeighbors(Node2D node)
    {
        List<Node2D> neighbors = new List<Node2D>(8);
        AddNeighbor(neighbors, node.GridX, node.GridY + 1);
        AddNeighbor(neighbors, node.GridX, node.GridY - 1);
        AddNeighbor(neighbors, node.GridX + 1, node.GridY);
        AddNeighbor(neighbors, node.GridX - 1, node.GridY);

        if (allowDiagonals)
        {
            // По диагонали можно, только если обе соседние клетки свободны — не срезаем углы.
            for (int dx = -1; dx <= 1; dx += 2)
            {
                for (int dy = -1; dy <= 1; dy += 2)
                {
                    if (IsFree(node.GridX + dx, node.GridY) && IsFree(node.GridX, node.GridY + dy))
                        AddNeighbor(neighbors, node.GridX + dx, node.GridY + dy);
                }
            }
        }

        return neighbors;
    }

    void AddNeighbor(List<Node2D> neighbors, int x, int y)
    {
        if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY)
            neighbors.Add(Grid[x, y]);
    }

    bool IsFree(int x, int y)
    {
        return x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY && IsPassable(Grid[x, y]);
    }

    // Узел клетки под точкой. Точки за пределами сетки прижимаются к краю.
    public Node2D NodeFromWorldPoint(Vector3 worldPosition)
    {
        if (Grid == null)
            return null;

        Vector3Int cell = layout.WorldToCell(worldPosition);
        int x = Mathf.Clamp(cell.x - bounds.xMin, 0, gridSizeX - 1);
        int y = Mathf.Clamp(cell.y - bounds.yMin, 0, gridSizeY - 1);
        return Grid[x, y];
    }

    // Ближайший проходимый узел (сам узел, если он проходим).
    public Node2D NearestWalkable(Node2D node, int maxRadius = 10)
    {
        if (node == null || IsPassable(node))
            return node;

        for (int r = 1; r <= maxRadius; r++)
        {
            Node2D best = null;
            float bestDistance = float.MaxValue;
            for (int x = node.GridX - r; x <= node.GridX + r; x++)
            {
                for (int y = node.GridY - r; y <= node.GridY + r; y++)
                {
                    bool onRing = Mathf.Abs(x - node.GridX) == r || Mathf.Abs(y - node.GridY) == r;
                    if (!onRing || !IsFree(x, y))
                        continue;

                    float distance = (Grid[x, y].worldPosition - node.worldPosition).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = Grid[x, y];
                    }
                }
            }
            if (best != null)
                return best;
        }
        return null;
    }

    public bool IsWalkable(Vector2 worldPosition)
    {
        if (Grid == null)
            return true;

        Vector3Int cell = layout.WorldToCell(worldPosition);
        return IsFree(cell.x - bounds.xMin, cell.y - bounds.yMin);
    }

    // Можно ли пройти по прямой между двумя точками, не задев непроходимых клеток.
    public bool IsLineWalkable(Vector2 from, Vector2 to)
    {
        if (Grid == null)
            return true;

        float step = Mathf.Min(cellWorldSize.x, cellWorldSize.y) * 0.3f;
        float distance = Vector2.Distance(from, to);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(step, 0.05f)));
        for (int i = 1; i <= samples; i++)
        {
            if (!IsWalkable(Vector2.Lerp(from, to, i / (float)samples)))
                return false;
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (Grid == null)
            return;

        Vector3 size = new Vector3(cellWorldSize.x, cellWorldSize.y, 0.1f) * 0.4f;
        foreach (Node2D n in Grid)
        {
            if (n.obstacle)
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            else if (n.dynamicObstacle)
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
            else
                Gizmos.color = new Color(1f, 1f, 1f, 0.5f);

            if (path != null && path.Contains(n))
                Gizmos.color = Color.black;
            Gizmos.DrawCube(n.worldPosition, size);
        }
    }
}
