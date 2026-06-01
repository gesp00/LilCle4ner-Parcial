using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// AStarPathfinder — Algoritmo A* desde cero.
/// Colocar en el mismo GameObject que AStarGrid.
/// Los agentes llaman a RequestPath() para obtener un camino.
/// Usa un Heap para eficiencia en mapas grandes.
/// </summary>
public class AStarPathfinder : MonoBehaviour
{
    public static AStarPathfinder Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─────────────────────────────────────────────
    // API PÚBLICA
    // Los agentes llaman esto para pedir un camino.
    // El resultado llega por callback para no bloquear el frame.
    // ─────────────────────────────────────────────

    public void RequestPath(Vector3 start, Vector3 end, System.Action<Vector3[], bool> callback)
    {
        StartCoroutine(FindPath(start, end, callback));
    }

    // ─────────────────────────────────────────────
    // A* CORE
    // ─────────────────────────────────────────────

    private IEnumerator FindPath(Vector3 startPos, Vector3 targetPos, System.Action<Vector3[], bool> callback)
    {
        AStarGrid grid = AStarGrid.Instance;

        Vector3[] waypoints   = new Vector3[0];
        bool      pathSuccess = false;

        Node startNode  = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        if (!startNode.Walkable)  startNode  = GetNearestWalkable(startNode);
        if (!targetNode.Walkable) targetNode = GetNearestWalkable(targetNode);

        if (startNode == targetNode)
        {
            callback(waypoints, false);
            yield break;
        }

        // Open list como Heap, Closed como HashSet
        Heap<Node>     openSet   = new Heap<Node>(grid.MaxSize);
        HashSet<Node>  closedSet = new HashSet<Node>();

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node current = openSet.RemoveFirst();
            closedSet.Add(current);

            // Llegamos al destino
            if (current == targetNode)
            {
                pathSuccess = true;
                break;
            }

            foreach (Node neighbour in grid.GetNeighbours(current))
            {
                if (!neighbour.Walkable || closedSet.Contains(neighbour)) continue;

                int newCostToNeighbour = current.GCost + GetDistance(current, neighbour);

                if (newCostToNeighbour < neighbour.GCost || !openSet.Contains(neighbour))
                {
                    neighbour.GCost  = newCostToNeighbour;
                    neighbour.HCost  = GetDistance(neighbour, targetNode);
                    neighbour.Parent = current;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                    else
                        openSet.UpdateItem(neighbour);
                }
            }
        }

        yield return null;

        if (pathSuccess)
            waypoints = RetracePath(startNode, targetNode);

        callback(waypoints, pathSuccess);
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    /// <summary>Reconstruye el camino desde el nodo final hasta el inicial.</summary>
    private Vector3[] RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node current = endNode;

        while (current != startNode)
        {
            path.Add(current);
            current = current.Parent;
        }

        path.Reverse();

        // Simplificar camino eliminando nodos colineales
        return SimplifyPath(path);
    }

    /// <summary>Elimina nodos intermedios en línea recta para un camino más limpio.</summary>
    private Vector3[] SimplifyPath(List<Node> path)
    {
        List<Vector3> waypoints  = new List<Vector3>();
        Vector2       oldDir     = Vector2.zero;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2 newDir = new Vector2(
                path[i].GridX - path[i - 1].GridX,
                path[i].GridZ - path[i - 1].GridZ);

            if (newDir != oldDir)
                waypoints.Add(path[i - 1].WorldPosition);

            oldDir = newDir;
        }

        // Agregar siempre el último punto
        if (path.Count > 0)
            waypoints.Add(path[path.Count - 1].WorldPosition);

        return waypoints.ToArray();
    }

    /// <summary>
    /// Distancia entre dos nodos.
    /// Diagonal = 14, Cardinal = 10 (evita usar float sqrt).
    /// </summary>
    private int GetDistance(Node a, Node b)
    {
        int dX = Mathf.Abs(a.GridX - b.GridX);
        int dZ = Mathf.Abs(a.GridZ - b.GridZ);

        return dX > dZ
            ? 14 * dZ + 10 * (dX - dZ)
            : 14 * dX + 10 * (dZ - dX);
    }

    /// <summary>Si el nodo de inicio/fin está en un obstáculo, busca el más cercano caminable.</summary>
    private Node GetNearestWalkable(Node node)
    {
        List<Node> neighbours = AStarGrid.Instance.GetNeighbours(node);
        foreach (Node n in neighbours)
            if (n.Walkable) return n;
        return node;
    }
}
