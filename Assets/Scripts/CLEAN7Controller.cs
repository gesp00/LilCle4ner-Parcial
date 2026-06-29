using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CLEAN-7 Controller — Movimiento isométrico 3D.
/// Requiere: CharacterController en el mismo GameObject.
/// Setup cámara: Y=45°, X≈55°, posición elevada. Asignar isoCameraReference en Inspector.
/// Parámetros Animator: "Speed" (Float), "IsCleaning" (Bool), "IsDead" (Bool)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CLEAN7Controller : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;       // velocidad al correr (Shift)
    [SerializeField] private float rotationSpeed = 600f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private float deceleration = 20f;
    [SerializeField] private float gravity = -20f;

    [Header("Cámara isométrica")]
    [Tooltip("Arrastrá la Main Camera aquí")]
    [SerializeField] private Transform isoCameraReference;

    [Header("Interacción")]
    [SerializeField] private float interactRange = 1.8f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Vida")]
    [SerializeField] private float maxHP = 3f;

    [Header("Eventos")]
    public UnityEvent onInteract;
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    // ─────────────────────────────────────────────
    // ESTADO INTERNO
    // ─────────────────────────────────────────────

    private CharacterController cc;
    private Animator anim;

    private Vector3 moveDir = Vector3.zero;
    private float verticalV = 0f;
    private float currentHP;
    private bool isAlive = true;
    private bool isCleaning = false;

    // ─────────────────────────────────────────────
    // PROPIEDADES PÚBLICAS
    // ─────────────────────────────────────────────

    public bool IsAlive => isAlive;
    public float MoveSpeed => moveSpeed;

    // ─────────────────────────────────────────────
    // INIT
    // ─────────────────────────────────────────────

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        currentHP = maxHP;
    }

    // ─────────────────────────────────────────────
    // LOOP
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (!isAlive) return;
        HandleMovementInput();
        HandleInteractInput();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;
        ApplyMovement();
    }

    // ─────────────────────────────────────────────
    // MOVIMIENTO ISOMÉTRICO
    // ─────────────────────────────────────────────

    private void HandleMovementInput()
    {
        // Bloquear movimiento mientras limpia
        if (isCleaning) return;

        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");
        Vector3 raw = new Vector3(ix, 0f, iz).normalized;

        // Shift para correr
        float targetSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : moveSpeed;

        if (raw.magnitude > 0.01f)
        {
            Vector3 forward = isoCameraReference != null
                ? Vector3.ProjectOnPlane(isoCameraReference.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 right = isoCameraReference != null
                ? Vector3.ProjectOnPlane(isoCameraReference.right, Vector3.up).normalized
                : Vector3.right;

            Vector3 targetDir = (forward * raw.z + right * raw.x).normalized;

            moveDir = Vector3.MoveTowards(moveDir, targetDir * targetSpeed, acceleration * Time.deltaTime);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(targetDir),
                rotationSpeed * Time.deltaTime);
        }
        else
        {
            moveDir = Vector3.MoveTowards(moveDir, Vector3.zero, deceleration * Time.deltaTime);
        }
    }

    private void ApplyMovement()
    {
        verticalV = cc.isGrounded ? -2f : verticalV + gravity * Time.fixedDeltaTime;
        cc.Move((moveDir + Vector3.up * verticalV) * Time.fixedDeltaTime);
    }

    // ─────────────────────────────────────────────
    // INTERACCIÓN
    // ─────────────────────────────────────────────

    private void HandleInteractInput()
    {
        if (Input.GetKeyDown(interactKey))
            TryInteract();
    }

    private void TryInteract()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, interactableLayer);

        ICleanable closest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<ICleanable>(out var c))
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < minDist) { minDist = d; closest = c; }
            }
        }

        if (closest != null)
        {
            closest.Clean(this);
            onInteract?.Invoke();
            StartCoroutine(PlayCleanAnimation());
        }
    }

    /// <summary>
    /// Activa la animación de limpieza y la desactiva cuando termina.
    /// La duración debe coincidir aproximadamente con el clip de Cleaning en Mixamo.
    /// </summary>
    private System.Collections.IEnumerator PlayCleanAnimation()
    {
        isCleaning = true;
        moveDir = Vector3.zero;   // frenar al limpiar

        if (anim != null) anim.SetBool("IsCleaning", true);

        // Esperar la duración del clip — ajustá este valor al largo de tu animación
        yield return new WaitForSeconds(cleaningAnimDuration);

        isCleaning = false;
        if (anim != null) anim.SetBool("IsCleaning", false);
    }

   
    [SerializeField] private float cleaningAnimDuration = 1.5f;

    // ─────────────────────────────────────────────
    // DAÑO
    // ─────────────────────────────────────────────

    public void TakeDamage(float amount)
    {
        if (!isAlive) return;
        currentHP -= amount;
        onDamaged?.Invoke();
        if (currentHP <= 0f) Die();
    }

    private void Die()
    {
        isAlive = false;
        onDeath?.Invoke();
        if (anim != null)
        {
            anim.SetBool("IsCleaning", false);
            anim.SetBool("IsDead", true);
        }
        GameManager.Instance?.TriggerDefeat();
    }

    // ─────────────────────────────────────────────
    // ANIMATOR
    // Speed (Int): 0 = quieto, 1 = caminando, 2 = corriendo
    // IsCrouching (Bool): true cuando camina (no corre)
    // IsCleaning (Bool): true durante animación de limpieza
    // IsDead (Bool): true al morir
    // ─────────────────────────────────────────────

    

    private void UpdateAnimator()
    {
        if (anim == null) return;

        anim.SetFloat("Speed", moveDir.magnitude);
        
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}