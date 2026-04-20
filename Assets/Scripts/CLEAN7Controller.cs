using UnityEngine;
using UnityEngine.Events;


public class CLEAN7Controller : MonoBehaviour
{



    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 600f;
    [SerializeField] private float acceleration = 15f;
    [SerializeField] private float deceleration = 20f;
    [SerializeField] private float gravity = -20f;

    [SerializeField] private Transform isoCameraReference;

    [SerializeField] private float interactRange = 1.8f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Vida")]
    [SerializeField] private float maxHP = 3f;

    [Header("Eventos")]
    public UnityEvent onInteract;
    public UnityEvent onDamaged;
    public UnityEvent onDeath;



    private CharacterController cc;
    private Animator anim;

    private Vector3 moveDir = Vector3.zero;
    private float verticalV = 0f;
    private float currentHP;
    private bool isAlive = true;



    public bool IsAlive => isAlive;
    public float MoveSpeed => moveSpeed;


    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        currentHP = maxHP;
    }

  

    private void Update()
    {
        if (!isAlive) return;
        HandleMovementInput();
        HandleInteractInput();
     
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;
        ApplyMovement();
    }

   
    private void HandleMovementInput()
    {
        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");
        Vector3 raw = new Vector3(ix, 0f, iz).normalized;

        if (raw.magnitude > 0.01f)
        {
            Vector3 forward = isoCameraReference != null
                ? Vector3.ProjectOnPlane(isoCameraReference.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 right = isoCameraReference != null
                ? Vector3.ProjectOnPlane(isoCameraReference.right, Vector3.up).normalized
                : Vector3.right;

            Vector3 targetDir = (forward * raw.z + right * raw.x).normalized;

            moveDir = Vector3.MoveTowards(moveDir, targetDir * moveSpeed, acceleration * Time.deltaTime);

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
        }
    }

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
        GameManager.Instance?.TriggerDefeat();
    }


  

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}