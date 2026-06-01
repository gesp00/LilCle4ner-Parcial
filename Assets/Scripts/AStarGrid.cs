using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AStarGrid — Genera una grilla de nodos sobre el mapa.
/// Colocar en un GameObject vacío "AStarGrid" en la escena.
/// Configurar gridWorldSize para que cubra el mapa completo (210 x 174).
/// La layer Unwalkable debe incluir paredes y obstáculos.
/// </summary>
public class AStarGrid : MonoBehaviour
{
    public static AStarGrid Instance { get; private set; }

    [Header("Grilla")]
    [Tooltip("Tamaño del mapa en unidades de Unity (X, Z)")]
    public Vector2 gridWorldSize = new Vector2(210f, 174f);

    [Tooltip("Radio de cada nodo — nodos más chicos = más precisión pero más costo")]
    public float nodeRadius = 1f;

    [Tooltip("Layer de obstáculos y paredes")]
    public LayerMask unwalkableMask;

    [Header("Debug")]
    public bool showGizmos = true;

    // ─────────────────────────────────────────────
    // NODOS
    // ─────────────────────────────────────────────

    public Node[,] Grid { get; private set; }

    private float nodeDiameter;
    private int   gridSizeX, gridSizeZ;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        nodeDiameter = nodeRadius * 2f;
        gridSizeX    = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeZ    = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);

        CreateGrid();
    }

    private void CreateGrid()
    {
        Grid = new Node[gridSizeX, gridSizeZ];

        // Esquina inferior izquierda del mapa
        Vector3 worldBottomLeft = transform.position
            - Vector3.right   * gridWorldSize.x * 0.5f
            - Vector3.forward * gridWorldSize.y * 0.5f;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                Vector3 worldPoint = worldBottomLeft
                    + Vector3.right   * (x * nodeDiameter + nodeRadius)
                    + Vector3.forward * (z * nodeDiameter + nodeRadius);

                // Raycast hacia arriba para detectar obstáculos
                bool walkable = !Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask);

                Grid[x, z] = new Node(walkable, worldPoint, x, z);
            }
        }
    }

    // ─────────────────────────────────────────────
    // UTILIDADES PÚBLICAS
    // ─────────────────────────────────────────────

    /// <summary>Devuelve el nodo más cercano a una posición del mundo.</summary>
    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = Mathf.Clamp01((worldPosition.x - (transform.position.x - gridWorldSize.x * 0.5f)) / gridWorldSize.x);
        float percentZ = Mathf.Clamp01((worldPosition.z - (transform.position.z - gridWorldSize.y * 0.5f)) / gridWorldSize.y);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int z = Mathf.RoundToInt((gridSizeZ - 1) * percentZ);

        return Grid[x, z];
    }

    /// <summary>Devuelve los nodos vecinos (8 direcciones).</summary>
    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                int checkX = node.GridX + dx;
                int checkZ = node.GridZ + dz;

                if (checkX >= 0 && checkX < gridSizeX && checkZ >= 0 && checkZ < gridSizeZ)
                    neighbours.Add(Grid[checkX, checkZ]);
            }
        }

        return neighbours;
    }

    public int MaxSize => gridSizeX * gridSizeZ;

    // ─────────────────────────────────────────────
    // GIZMOS — visualización en el editor
    // ─────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1f, gridWorldSize.y));

        if (Grid == null) return;

        foreach (Node n in Grid)
        {
            Gizmos.color = n.Walkable ? new Color(1f, 1f, 1f, 0.1f) : new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawCube(n.WorldPosition, Vector3.one * (nodeDiameter - 0.1f));
        }
    }
}

// =====================================================
// NODO
// =====================================================

public class Node : IHeapItem<Node>
{
    public bool    Walkable;
    public Vector3 WorldPosition;
    public int     GridX, GridZ;

    public int GCost;   // costo desde el inicio
    public int HCost;   // heurística hasta el destino
    public int FCost => GCost + HCost;

    public Node Parent;

    private int heapIndex;

    public Node(bool walkable, Vector3 worldPos, int gridX, int gridZ)
    {
        Walkable      = walkable;
        WorldPosition = worldPos;
        GridX         = gridX;
        GridZ         = gridZ;
    }

    public int HeapIndex
    {
        get => heapIndex;
        set => heapIndex = value;
    }

    public int CompareTo(Node other)
    {
        int compare = FCost.CompareTo(other.FCost);
        if (compare == 0) compare = HCost.CompareTo(other.HCost);
        return -compare;   // negativo porque el Heap es de mínimo
    }
}

// =====================================================
// HEAP — para que A* sea eficiente con mapas grandes
// =====================================================

public interface IHeapItem<T> : System.IComparable<T>
{
    int HeapIndex { get; set; }
}

public class Heap<T> where T : IHeapItem<T>
{
    private T[] items;
    private int currentItemCount;

    public Heap(int maxHeapSize)
    {
        items = new T[maxHeapSize];
    }

    public void Add(T item)
    {
        item.HeapIndex = currentItemCount;
        items[currentItemCount] = item;
        SortUp(item);
        currentItemCount++;
    }

    public T RemoveFirst()
    {
        T firstItem = items[0];
        currentItemCount--;
        items[0] = items[currentItemCount];
        items[0].HeapIndex = 0;
        SortDown(items[0]);
        return firstItem;
    }

    public void UpdateItem(T item) => SortUp(item);

    public int  Count    => currentItemCount;
    public bool Contains(T item) => Equals(items[item.HeapIndex], item);

    private void SortDown(T item)
    {
        while (true)
        {
            int childL = item.HeapIndex * 2 + 1;
            int childR = item.HeapIndex * 2 + 2;
            int swapIndex;

            if (childL >= currentItemCount) return;

            swapIndex = childL;
            if (childR < currentItemCount && items[childL].CompareTo(items[childR]) < 0)
                swapIndex = childR;

            if (item.CompareTo(items[swapIndex]) < 0)
                Swap(item, items[swapIndex]);
            else
                return;
        }
    }

    private void SortUp(T item)
    {
        int parentIndex = (item.HeapIndex - 1) / 2;
        while (true)
        {
            T parent = items[parentIndex];
            if (item.CompareTo(parent) > 0)
                Swap(item, parent);
            else
                break;
            parentIndex = (item.HeapIndex - 1) / 2;
        }
    }

    private void Swap(T a, T b)
    {
        items[a.HeapIndex] = b;
        items[b.HeapIndex] = a;
        (a.HeapIndex, b.HeapIndex) = (b.HeapIndex, a.HeapIndex);
    }
}
