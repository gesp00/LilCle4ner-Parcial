using UnityEngine;
using UnityEngine.Events;

public class CleaningManager : MonoBehaviour
{
    public static CleaningManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private int totalCleanables = 0;

    [Header("Eventos")]
    public UnityEvent onAllCleaned;
    public UnityEvent<int> onScoreChanged;

    private int score = 0;
    private int cleanedCount = 0;
    private int totalInScene = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (totalCleanables == 0)
        {
            totalInScene += FindObjectsByType<RadioactiveDebris>(FindObjectsSortMode.None).Length;
            totalInScene += FindObjectsByType<BrokenEnergyCell>(FindObjectsSortMode.None).Length;
        }
        else
        {
            totalInScene = totalCleanables;
        }

        Debug.Log($"[CleaningManager] Total objetos a limpiar: {totalInScene}");
    }

    public void RegisterClean(int points)
    {
        cleanedCount++;
        score += points;

        onScoreChanged?.Invoke(score);

        Debug.Log($"[CleaningManager] Limpiados: {cleanedCount}/{totalInScene}");

        if (cleanedCount >= totalInScene)
            onAllCleaned?.Invoke();
    }

    public int Score => score;
    public int Remaining => Mathf.Max(0, totalInScene - cleanedCount);
    public bool AllCleaned => cleanedCount >= totalInScene;
}