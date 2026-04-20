using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RadioactiveDebris : MonoBehaviour, ICleanable
{
    [Header("Configuración")]
    [SerializeField] private float cleanTime = 50f;   
    [SerializeField] private int scoreValue = 10;

    [Header("Eventos")]
    public UnityEvent onCleaned;

    public bool IsCleaned { get; private set; } = false;

    public void Clean(CLEAN7Controller cleaner)
    {
        if (IsCleaned) return;

        IsCleaned = true;


        CleaningManager.Instance?.RegisterClean(scoreValue);

        onCleaned?.Invoke();


        Destroy(gameObject, 0.1f); 
    }
}
