using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PirateWanderer — Patrullero errático.
/// FSM: WANDER → INTERCEPT → ATTACK
/// - WANDER:    Deambula por el mapa con Wander steering.
///              Si pierde el camino, pide uno nuevo vía A*.
/// - INTERCEPT: Al ver al jugador, usa Arrive para cortarle el paso
///              (se dirige a donde VA el jugador, no donde ESTÁ).
/// - ATTACK:    Rango corto, ataca rápido.
///
/// Steering usados: Wander, Arrive, Seek.
/// Pathfinding: A* para encontrar destinos de wander aleatorios.
/// Requiere: NavMeshAgent, Rigidbody (kinematic), AStarPathfinder en la escena.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class PirateWanderer : MonoBehaviour
{
    public enum State { Wander, Intercept, Attack }

    [Header("Referencias")]
    [SerializeField] private CLEAN7Controller player;

    [Header("Line of Sight")]
    [SerializeField] private float sightRange = 12f;
    [SerializeField] private float sightAngle = 110f;   // FOV más amplio — es un patrullero
    [SerializeField] private LayerMask obstacleMask;

    [Header("Wander")]
    [SerializeField] private float wanderSpeed = 3f;
    [SerializeField] private float wanderCircleDistance = 2f;
    [SerializeField] private float wanderCircleRadius = 1.5f;
    [SerializeField] private float wanderAngleChange = 90f;
    [SerializeField] private float newWanderDestEvery = 4f;   // pedir nuevo path A* cada N segundos

    [Header("Intercepción")]
    [SerializeField] private float interceptSpeed = 5.5f;
    [SerializeField] private float interceptAhead = 1.5f;    // cuánto predice adelante al jugador
    [SerializeField] private float slowingRadius = 3f;

    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Estado")]
    [SerializeField] private State currentState = State.Wander;

    // ─────────────────────────────────────────────
    // INTERNO
    // ─────────────────────────────────────────────

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Animator anim;

    private Vector3 velocity = Vector3.zero;
    private float wanderAngle = 0f;
    private float attackTimer = 0f;
    private float wanderTimer = 0f;

    // Path A*
    private Vector3[] currentPath = new Vector3[0];
    private int pathIndex = 0;
    private bool hasPath = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();

        rb.isKinematic = true;
        rb.freezeRotation = true;

        if (player == null)
            player = FindFirstObjectByType<CLEAN7Controller>();
    }

    private void Start()
    {
        agent.updateRotation = false;   // nosotros rotamos manualmente
        agent.speed = wanderSpeed;
        wanderAngle = Random.Range(0f, Mathf.PI * 2f);
        RequestNewWanderDestination();
    }

    private void Update()
    {
        if (player == null || !player.IsAlive) return;

        attackTimer -= Time.deltaTime;
        wanderTimer -= Time.deltaTime;

        switch (currentState)
        {
            case State.Wander: UpdateWander(); break;
            case State.Intercept: UpdateIntercept(); break;
            case State.Attack: UpdateAttack(); break;
        }

        UpdateAnimator();
    }

    // ─────────────────────────────────────────────
    // ESTADOS
    // ─────────────────────────────────────────────

    private void UpdateWander()
    {
        if (CheckLoS())
        {
            EnterState(State.Intercept);
            return;
        }

        // Solo pedimos un nuevo destino si NO tenemos ruta actual.
        // Usamos el timer como tiempo de espera (cooldown) antes de buscar otro punto.
        if (!hasPath)
        {
            if (wanderTimer <= 0f)
            {
                RequestNewWanderDestination();
                wanderTimer = newWanderDestEvery;
            }
        }

        if (hasPath && currentPath != null && currentPath.Length > 0)
        {
            Vector3 waypoint = currentPath[pathIndex];
            waypoint.y = transform.position.y;

            float distToWaypoint = Vector3.Distance(transform.position, waypoint);

            // Aumentamos la tolerancia a 1.5f para que no se quede orbitando el nodo
            if (distToWaypoint < 1.5f)
            {
                pathIndex++;
                if (pathIndex >= currentPath.Length)
                {
                    hasPath = false;
                    wanderTimer = 1.5f; // Espera un poco en el lugar antes de buscar otro destino
                    return;
                }
            }

            Vector3 arriveForce = SteeringBehaviors.Arrive(
                transform.position,
                waypoint,
                velocity,
                wanderSpeed,
                slowingRadius: 1.5f);

            // Reducimos drásticamente la fuerza del Wander mientras sigue un Path 
            // para que tenga un caminar natural pero no pierda el rumbo.
            Vector3 wanderForce = SteeringBehaviors.Wander(
                transform.position,
                velocity,
                wanderSpeed * 0.1f, // Reducido al 10%
                wanderCircleDistance,
                wanderCircleRadius,
                wanderAngleChange,
                ref wanderAngle);

            Vector3 totalForce = SteeringBehaviors.Truncate(arriveForce + wanderForce, wanderSpeed);
            ApplySteering(totalForce);
        }
        else
        {
            // Wander puro si no tiene ruta y está esperando el cooldown
            Vector3 wanderForce = SteeringBehaviors.Wander(
                transform.position,
                velocity,
                wanderSpeed,
                wanderCircleDistance,
                wanderCircleRadius,
                wanderAngleChange,
                ref wanderAngle);

            ApplySteering(SteeringBehaviors.Truncate(wanderForce, wanderSpeed));
        }
    }

    private void UpdateIntercept()
    {
        if (!CheckLoS())
        {
            EnterState(State.Wander);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (dist <= attackRange)
        {
            EnterState(State.Attack);
            return;
        }

        // Limitamos la velocidad predicha del jugador para que no calcule posiciones a kilómetros si el jugador usa un Dash
        Vector3 playerVel = player.GetComponent<CharacterController>()?.velocity ?? Vector3.zero;
        Vector3 clampedPlayerVel = Vector3.ClampMagnitude(playerVel, interceptSpeed);

        Vector3 predictedTarget = player.transform.position + clampedPlayerVel * interceptAhead;

        Vector3 arriveForce = SteeringBehaviors.Arrive(
            transform.position,
            predictedTarget,
            velocity,
            interceptSpeed,
            slowingRadius);

        ApplySteering(SteeringBehaviors.Truncate(arriveForce, interceptSpeed));
    }

    private void UpdateAttack()
    {
        if (!CheckLoS())
        {
            EnterState(State.Wander);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (dist > attackRange * 1.3f)
        {
            EnterState(State.Intercept);
            return;
        }

        // Seek suave para mantenerse pegado al jugador
        Vector3 seekForce = SteeringBehaviors.Seek(
            transform.position,
            player.transform.position,
            velocity,
            wanderSpeed * 0.5f);

        ApplySteering(SteeringBehaviors.Truncate(seekForce, wanderSpeed));

        LookAt(player.transform.position);

        if (attackTimer <= 0f)
        {
            player.TakeDamage(attackDamage);
            attackTimer = attackCooldown;
            anim?.SetTrigger("Attack");
        }
    }

    // ─────────────────────────────────────────────
    // STEERING
    // ─────────────────────────────────────────────

    private void ApplySteering(Vector3 force)
    {
        velocity += force * Time.deltaTime;
        velocity = SteeringBehaviors.Truncate(velocity, interceptSpeed);
        // Mantenemos la Y en 0 para evitar que el agente intente volar o hundirse
        velocity.y = 0f;

        if (velocity.magnitude > 0.01f)
        {
            // Chequeo de seguridad: ¿Está el agente tocando el NavMesh?
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.Move(velocity * Time.deltaTime);
            }
            else
            {
                // Si no está en el NavMesh, dejamos un aviso en consola 
                // para que sepas qué enemigo está fallando.
                Debug.LogWarning($"{gameObject.name} no está tocando el NavMesh o no está activo.");
            }

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(velocity),
                10f * Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────
    // PATHFINDING A* — destino aleatorio para wander
    // ─────────────────────────────────────────────

    private void RequestNewWanderDestination()
    {
        // Elegir un punto aleatorio en el mapa
        Vector3 randomDir = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)).normalized;

        Vector3 destination = transform.position + randomDir * Random.Range(8f, 20f);

        // Verificar que el punto está en el NavMesh
        if (UnityEngine.AI.NavMesh.SamplePosition(destination, out var hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            destination = hit.position;

        AStarPathfinder.Instance?.RequestPath(transform.position, destination, OnPathFound);
    }

    private void OnPathFound(Vector3[] path, bool success)
    {
        if (success && path.Length > 0)
        {
            currentPath = path;
            pathIndex = 0;
            hasPath = true;
        }
        else
        {
            hasPath = false;
        }
    }

    // ─────────────────────────────────────────────
    // LINE OF SIGHT
    // ─────────────────────────────────────────────

    private bool CheckLoS()
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        float dist = toPlayer.magnitude;

        if (dist <= 2f) return true;   // detección cercana sin ángulo
        if (dist > sightRange) return false;

        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
        if (angle > sightAngle * 0.5f) return false;

        Vector3 origin = transform.position + Vector3.up;
        Vector3 dir = (player.transform.position + Vector3.up * 0.5f - origin).normalized;

        return !Physics.Raycast(origin, dir, dist, obstacleMask);
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    private void EnterState(State newState)
    {
        currentState = newState;
        anim?.SetInteger("State", (int)newState);
    }

    private void LookAt(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (anim == null) return;
        anim.SetFloat("Speed", velocity.magnitude);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}