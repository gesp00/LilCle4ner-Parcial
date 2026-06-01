using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PirateSniper — Francotirador.
/// FSM: SNIPE → EVADE → RELOCATE
/// - SNIPE:    Quieto en posición, dispara a distancia si tiene LoS.
/// - EVADE:    Si el jugador se acerca demasiado, usa Evade (steering) para escapar.
/// - RELOCATE: Busca un nuevo punto de francotirador vía A* cuando fue expulsado.
///
/// Steering usados: Evade, Flee.
/// Pathfinding: A* propio para reubicarse.
/// Requiere: NavMeshAgent (para moverse al reubicarse), AStarPathfinder en la escena.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class PirateSniper : MonoBehaviour
{
    public enum State { Snipe, Evade, Relocate }

    [Header("Referencias")]
    [SerializeField] private CLEAN7Controller player;
    [SerializeField] private Transform[]      sniperPositions;   // puntos donde puede apostarse

    [Header("Line of Sight")]
    [SerializeField] private float sightRange    = 18f;
    [SerializeField] private float sightAngle    = 60f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Combate")]
    [SerializeField] private float attackRange    = 15f;
    [SerializeField] private float attackDamage   = 1f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float dangerRange    = 5f;    // distancia que activa Evade

    [Header("Steering")]
    [SerializeField] private float maxSpeed       = 6f;
    [SerializeField] private float maxForce       = 8f;
    [SerializeField] private float steeringWeight = 5f;    // cuánto influye el steering vs NavMesh

    [Header("Estado")]
    [SerializeField] private State currentState = State.Snipe;

    // ─────────────────────────────────────────────
    // INTERNO
    // ─────────────────────────────────────────────

    private NavMeshAgent agent;
    private Rigidbody    rb;
    private Animator     anim;

    private Vector3 velocity       = Vector3.zero;
    private float   attackTimer    = 0f;
    private int     currentSnipePos = 0;

    // Path A*
    private Vector3[] currentPath  = new Vector3[0];
    private int       pathIndex    = 0;
    private bool      hasPath      = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb    = GetComponent<Rigidbody>();
        anim  = GetComponent<Animator>();

        rb.freezeRotation = true;

        if (player == null)
            player = FindFirstObjectByType<CLEAN7Controller>();
    }

    private void Start()
    {
        // Ir a la primera posición de francotirador
        GoToSniperPosition(0);
        agent.updateRotation = false;
    }

    private void Update()
    {
        if (player == null || !player.IsAlive) return;

        attackTimer -= Time.deltaTime;

        switch (currentState)
        {
            case State.Snipe:    UpdateSnipe();    break;
            case State.Evade:    UpdateEvade();    break;
            case State.Relocate: UpdateRelocate(); break;
        }

        UpdateAnimator();
    }

    // ─────────────────────────────────────────────
    // ESTADOS
    // ─────────────────────────────────────────────

    private void UpdateSnipe()
    {
        // Si el jugador está demasiado cerca → Evade
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist < dangerRange)
        {
            EnterState(State.Evade);
            return;
        }

        // Mirar al jugador
        LookAt(player.transform.position);

        // Disparar si tiene LoS y está en rango
        if (dist <= attackRange && CheckLoS())
        {
            if (attackTimer <= 0f)
            {
                player.TakeDamage(attackDamage);
                attackTimer = attackCooldown;
                anim?.SetTrigger("Attack");
            }
        }
    }

    private void UpdateEvade()
    {
        float dist = Vector3.Distance(transform.position, player.transform.position);

        // Si se alejó suficiente → buscar nueva posición de sniper
        if (dist > dangerRange * 2f)
        {
            int nextPos = GetFarthestSniperPosition();
            GoToSniperPosition(nextPos);
            EnterState(State.Relocate);
            return;
        }

        // Steering: Evade (predice posición futura del jugador)
        Vector3 playerVelocity = player.GetComponent<CharacterController>()?.velocity ?? Vector3.zero;

        Vector3 evadeForce = SteeringBehaviors.Evade(
            transform.position,
            player.transform.position,
            playerVelocity,
            velocity,
            maxSpeed);

        // También aplicar Flee puro para reforzar el alejamiento
        Vector3 fleeForce = SteeringBehaviors.Flee(
            transform.position,
            player.transform.position,
            velocity,
            maxSpeed);

        Vector3 totalForce = SteeringBehaviors.Truncate(evadeForce + fleeForce * 0.5f, maxForce);
        ApplySteering(totalForce);
    }

    private void UpdateRelocate()
    {
        if (!hasPath || currentPath.Length == 0)
        {
            EnterState(State.Snipe);
            return;
        }

        // Seguir el path A*
        Vector3 target = currentPath[pathIndex];
        target.y = transform.position.y;

        float dist = Vector3.Distance(transform.position, target);
        if (dist < 0.8f)
        {
            pathIndex++;
            if (pathIndex >= currentPath.Length)
            {
                hasPath = false;
                EnterState(State.Snipe);
                return;
            }
        }

        // Usar Arrive para llegar suavemente al punto del path
        Vector3 arriveForce = SteeringBehaviors.Arrive(
            transform.position,
            target,
            velocity,
            maxSpeed,
            slowingRadius: 2f);

        ApplySteering(SteeringBehaviors.Truncate(arriveForce, maxForce));
    }

    // ─────────────────────────────────────────────
    // STEERING — aplicar fuerza al movimiento
    // ─────────────────────────────────────────────

    private void ApplySteering(Vector3 force)
    {
        velocity += force * Time.deltaTime;
        velocity  = SteeringBehaviors.Truncate(velocity, maxSpeed);
        velocity.y = 0f;

        if (velocity.magnitude > 0.01f)
        {
            // Mover con NavMeshAgent para respetar el NavMesh
            agent.Move(velocity * Time.deltaTime);

            // Rotar hacia donde se mueve
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(velocity),
                10f * Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────
    // PATHFINDING A*
    // ─────────────────────────────────────────────

    private void GoToSniperPosition(int index)
    {
        if (sniperPositions == null || sniperPositions.Length == 0) return;

        currentSnipePos = index % sniperPositions.Length;
        Vector3 destination = sniperPositions[currentSnipePos].position;

        AStarPathfinder.Instance?.RequestPath(transform.position, destination, OnPathFound);
    }

    private void OnPathFound(Vector3[] path, bool success)
    {
        if (success && path.Length > 0)
        {
            currentPath = path;
            pathIndex   = 0;
            hasPath     = true;
        }
        else
        {
            hasPath = false;
        }
    }

    /// <summary>Devuelve el índice de la posición de sniper más lejos del jugador.</summary>
    private int GetFarthestSniperPosition()
    {
        int   best     = 0;
        float bestDist = 0f;

        for (int i = 0; i < sniperPositions.Length; i++)
        {
            float d = Vector3.Distance(sniperPositions[i].position, player.transform.position);
            if (d > bestDist) { bestDist = d; best = i; }
        }

        return best;
    }

    // ─────────────────────────────────────────────
    // LINE OF SIGHT
    // ─────────────────────────────────────────────

    private bool CheckLoS()
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        float   dist     = toPlayer.magnitude;

        if (dist > sightRange) return false;

        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
        if (angle > sightAngle * 0.5f) return false;

        Vector3 origin = transform.position + Vector3.up;
        Vector3 dir    = (player.transform.position + Vector3.up * 0.5f - origin).normalized;

        return !Physics.Raycast(origin, dir, dist, obstacleMask);
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    private void EnterState(State newState)
    {
        currentState = newState;

        if (newState == State.Evade || newState == State.Relocate)
            agent.isStopped = true;   // el steering toma el control
        else
            agent.isStopped = true;   // en Snipe también quieto

        anim?.SetInteger("State", (int)newState);
    }

    private void LookAt(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 5f * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (anim == null) return;
        anim.SetFloat("Speed", velocity.magnitude);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dangerRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
