using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PirateAI — FSM con Line of Sight.
/// Estados: PATROL → CHASE → ATTACK
/// Requiere: NavMeshAgent, NavMesh horneado en la escena.
/// Asignar "player" (CLEAN7Controller) y waypoints de patrulla en el Inspector.
/// obstacleMask: asignar la layer de las paredes/obstáculos.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PirateAI : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // ESTADOS
    // ─────────────────────────────────────────────

    public enum State { Patrol, Chase, Attack }

    [Header("Estado actual (solo lectura en runtime)")]
    [SerializeField] private State currentState = State.Patrol;

    // ─────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────

    [Header("Referencias")]
    [SerializeField] private CLEAN7Controller player;
    [SerializeField] private Transform[] patrolWaypoints;

    [Header("Line of Sight")]
    [SerializeField] private float sightRange = 10f;    // distancia máxima de visión
    [SerializeField] private float sightAngle = 90f;    // ángulo total del cono (FOV)
    [SerializeField] private float hearRange = 3f;     // detección sin LoS (radio de ruido)
    [SerializeField] private LayerMask obstacleMask;     // paredes y objetos que bloquean visión

    [Header("Comportamiento")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float waypointTolerance = 0.5f;

    // ─────────────────────────────────────────────
    // ESTADO INTERNO
    // ─────────────────────────────────────────────

    private NavMeshAgent agent;
    //private Animator anim;

    private int currentWaypoint = 0;
    private float attackTimer = 0f;

    private bool playerVisible = false;

    // ─────────────────────────────────────────────
    // INIT
    // ─────────────────────────────────────────────

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        //anim = GetComponent<Animator>();

        // Buscar al jugador automáticamente si no está asignado
        if (player == null)
            player = FindFirstObjectByType<CLEAN7Controller>();
    }

    private void Start()
    {
        StartCoroutine(InitDelayed());
    }

    private System.Collections.IEnumerator InitDelayed()
    {
        yield return null;
        if (agent.isOnNavMesh)
            EnterState(State.Patrol);
        else
            Debug.LogWarning($"[PirateAI] {gameObject.name} no está sobre un NavMesh. Verificá que el NavMesh esté horneado.", this);
    }

    // ─────────────────────────────────────────────
    // LOOP PRINCIPAL
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (player == null || !player.IsAlive) return;
        if (!agent.isOnNavMesh) return;

        playerVisible = CheckLineOfSight();
        attackTimer -= Time.deltaTime;

        switch (currentState)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(); break;
            case State.Attack: UpdateAttack(); break;
        }

        UpdateAnimator();
    }

    // ─────────────────────────────────────────────
    // LINE OF SIGHT
    // Primero chequea distancia y ángulo (cono),
    // luego lanza un Raycast para verificar obstáculos.
    // ─────────────────────────────────────────────

    private bool CheckLineOfSight()
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        float distance = toPlayer.magnitude;

        // 1. Radio de escucha (detección cercana sin ángulo)
        if (distance <= hearRange)
            return true;

        // 2. Fuera del rango de visión
        if (distance > sightRange)
            return false;

        // 3. Fuera del cono de visión
        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
        if (angle > sightAngle * 0.5f)
            return false;

        // 4. Raycast — ¿hay obstáculos entre el pirata y el jugador?
        // Usamos obstacleMask (paredes, objetos) para ver si algo bloquea la línea.
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 targetPos = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (targetPos - origin).normalized;
        float dist = Vector3.Distance(origin, targetPos);

        if (Physics.Raycast(origin, dir, dist, obstacleMask))
            return false;   // hay una pared u obstáculo en medio

        return true;
    }

    // ─────────────────────────────────────────────
    // ESTADOS
    // ─────────────────────────────────────────────

    // ── PATROL ──────────────────────────────────

    private void UpdatePatrol()
    {
        if (playerVisible)
        {
            EnterState(State.Chase);
            return;
        }

        if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

        // Si llegó al waypoint actual, avanzar al siguiente
        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            currentWaypoint = (currentWaypoint + 1) % patrolWaypoints.Length;
            agent.SetDestination(patrolWaypoints[currentWaypoint].position);
        }
    }

    // ── CHASE ────────────────────────────────────
    // Persigue mientras tenga LoS. Al perderlo, vuelve a patrullar.

    private void UpdateChase()
    {
        if (!playerVisible)
        {
            EnterState(State.Patrol);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (dist <= attackRange)
        {
            EnterState(State.Attack);
            return;
        }

        agent.SetDestination(player.transform.position);
    }

    // ── ATTACK ───────────────────────────────────
    // Se planta y dispara. Si el jugador se aleja, vuelve a perseguir.

    private void UpdateAttack()
    {
        if (!playerVisible)
        {
            EnterState(State.Patrol);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        // Jugador se alejó del rango de ataque
        if (dist > attackRange * 1.2f)   // pequeño margen para evitar flip-flop
        {
            EnterState(State.Chase);
            return;
        }

        // Mirar al jugador mientras ataca
        Vector3 lookDir = (player.transform.position - transform.position).normalized;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(lookDir), 360f * Time.deltaTime);

        // Disparar según cooldown
        if (attackTimer <= 0f)
        {
            player.TakeDamage(attackDamage);
            attackTimer = attackCooldown;
            //anim?.SetTrigger("Attack");
        }
    }

    // ─────────────────────────────────────────────
    // TRANSICIONES DE ESTADO
    // ─────────────────────────────────────────────

    private void EnterState(State newState)
    {
        currentState = newState;

        if (agent.isOnNavMesh)
        {
            switch (newState)
            {
                case State.Patrol:
                    agent.speed = patrolSpeed;
                    agent.isStopped = false;
                    // Retomar patrulla desde el waypoint más cercano
                    if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                        agent.SetDestination(patrolWaypoints[currentWaypoint].position);
                    break;

                case State.Chase:
                    agent.speed = chaseSpeed;
                    agent.isStopped = false;
                    break;

                case State.Attack:
                    agent.isStopped = true;
                    agent.ResetPath();
                    break;
            }
        }

        //if (anim != null)
        //    anim.SetInteger("State", (int)newState);
    }

    // ─────────────────────────────────────────────
    // ANIMATOR
    // Parámetros sugeridos: "Speed" (float), "State" (int), "Attack" (trigger)
    // State: 0=Patrol, 1=Chase, 2=Attack
    // ─────────────────────────────────────────────

    private void UpdateAnimator()
    {
        //if (anim == null) return;
        //anim.SetFloat("Speed", agent.velocity.magnitude);
    }

    // ─────────────────────────────────────────────
    // GIZMOS — visualización en el editor
    // ─────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Rango de visión
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Cono de visión
        Vector3 leftBound = Quaternion.Euler(0, -sightAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, sightAngle * 0.5f, 0) * transform.forward;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawRay(transform.position, leftBound * sightRange);
        Gizmos.DrawRay(transform.position, rightBound * sightRange);

        // Rango de escucha
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hearRange);

        // Rango de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);


    }
}