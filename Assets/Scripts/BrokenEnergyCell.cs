using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BrokenEnergyCell : MonoBehaviour, ICleanable
{
    [Header("Configuración")]
    [SerializeField] private float radiationRadius = 3f;
    [SerializeField] private float radiationDamage = 0.5f;   
    [SerializeField] private float radiationTickRate = 1f;    
    [SerializeField] private int scoreValue = 25;

    [Header("Efectos")]
    [SerializeField] private GameObject radiationFX;   
    [SerializeField] private GameObject deactivatedFX;
    [SerializeField] private AudioClip deactivateSound;

    [Header("Eventos")]
    public UnityEvent onDeactivated;

    public bool IsCleaned { get; private set; } = false;

    private CLEAN7Controller player;
    private float tickTimer = 0f;

    private void Start()
    {
        player = FindObjectOfType<CLEAN7Controller>();
        if (radiationFX != null) radiationFX.SetActive(true);
    }

    private void Update()
    {
        if (IsCleaned || player == null) return;

       
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= radiationRadius)
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                player.TakeDamage(radiationDamage);
                tickTimer = radiationTickRate;
            }
        }
    }

    public void Clean(CLEAN7Controller cleaner)
    {
        if (IsCleaned) return;

        IsCleaned = true;

        CleaningManager.Instance.RegisterClean(scoreValue);

        onDeactivated?.Invoke();

        if (radiationFX != null) radiationFX.SetActive(false);
        if (deactivatedFX != null) Instantiate(deactivatedFX, transform.position, Quaternion.identity);
        if (deactivateSound != null) AudioSource.PlayClipAtPoint(deactivateSound, transform.position);

        
        enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, radiationRadius);
    }
}

