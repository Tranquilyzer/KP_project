using UnityEngine;
using System.Collections.Generic;

// Поиск пути A*. Статический FindPath хранит стоимости отдельно для каждого поиска,
// поэтому им могут одновременно пользоваться сколько угодно NPC.
public class Pathfinding2D : MonoBehaviour
{
    public Transform seeker, target;
    public GameObject GridOwner;
    Grid2D grid;

    void Start()
    {
        grid = GridOwner != null ? GridOwner.GetComponent<Grid2D>() : Grid2D.Instance;
    }

    // Старый вариант вызова: результат кладётся в grid.path.
    public void FindPath(Vector3 startPos, Vector3 targetPos)
    {
        if (grid != null)
            grid.path = FindPath(grid, startPos, targetPos);
    }

    public static List<Node2D> FindPath(Grid2D grid, Vector3 startPos, Vector3 targetPos, int maxIterations = 5000)
    {
        if (grid == null || grid.Grid == null)
            return null;

        Node2D startNode = grid.NearestWalkable(grid.NodeFromWorldPoint(startPos));
        Node2D targetNode = grid.NearestWalkable(grid.NodeFromWorldPoint(targetPos));
        if (startNode == null || targetNode == null)
            return null;
        if (startNode == targetNode)
            return new List<Node2D> { targetNode };

        List<Node2D> openList = new List<Node2D> { startNode };
        HashSet<Node2D> openSet = new HashSet<Node2D> { startNode };
        HashSet<Node2D> closedSet = new HashSet<Node2D>();
        Dictionary<Node2D, int> gCost = new Dictionary<Node2D, int> { { startNode, 0 } };
        Dictionary<Node2D, Node2D> parent = new Dictionary<Node2D, Node2D>();

        int iterations = 0;
        while (openList.Count > 0 && iterations++ < maxIterations)
        {
            int bestIndex = 0;
            int bestH = GetDistance(openList[0], targetNode);
            int bestF = gCost[openList[0]] + bestH;
            for (int i = 1; i < openList.Count; i++)
            {
                int h = GetDistance(openList[i], targetNode);
                int f = gCost[openList[i]] + h;
                if (f < bestF || (f == bestF && h < bestH))
                {
                    bestIndex = i;
                    bestF = f;
                    bestH = h;
                }
            }

            Node2D node = openList[bestIndex];
            openList.RemoveAt(bestIndex);
            openSet.Remove(node);
            closedSet.Add(node);

            if (node == targetNode)
                return RetracePath(parent, startNode, targetNode);

            foreach (Node2D neighbour in grid.GetNeighbors(node))
            {
                if (!grid.IsPassable(neighbour) || closedSet.Contains(neighbour))
                    continue;

                int cost = gCost[node] + GetDistance(node, neighbour);
                if (gCost.TryGetValue(neighbour, out int oldCost) && cost >= oldCost)
                    continue;

                gCost[neighbour] = cost;
                parent[neighbour] = node;
                if (openSet.Add(neighbour))
                    openList.Add(neighbour);
            }
        }

        return null;
    }

    static List<Node2D> RetracePath(Dictionary<Node2D, Node2D> parent, Node2D startNode, Node2D endNode)
    {
        List<Node2D> path = new List<Node2D>();
        Node2D currentNode = endNode;
        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = parent[currentNode];
        }
        path.Reverse();
        return path;
    }

    static int GetDistance(Node2D nodeA, Node2D nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}
