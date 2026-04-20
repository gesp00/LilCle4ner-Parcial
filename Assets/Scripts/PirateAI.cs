using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(NavMeshAgent))]
public class PirateAI : MonoBehaviour
{
   
    public enum State { Patrol, Chase, Attack }

    [Header("Estado actual (solo lectura en runtime)")]
    [SerializeField] private State currentState = State.Patrol;

    [Header("Referencias")]
    [SerializeField] private CLEAN7Controller player;
    [SerializeField] private Transform[] patrolWaypoints;

    [Header("Line of Sight")]
    [SerializeField] private float sightRange = 10f;    
    [SerializeField] private float sightAngle = 90f;    
    [SerializeField] private float hearRange = 3f;    
    [SerializeField] private LayerMask obstacleMask;

    [Header("Comportamiento")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float waypointTolerance = 0.5f;

   
    private NavMeshAgent agent;

    private int currentWaypoint = 0;
    private float attackTimer = 0f;

    private bool playerVisible = false;

   

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        

      
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

        
    }

   
    private bool CheckLineOfSight()
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        float distance = toPlayer.magnitude;

        
        if (distance <= hearRange)
            return true;

     
        if (distance > sightRange)
            return false;

       
        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
        if (angle > sightAngle * 0.5f)
            return false;

       
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 targetPos = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (targetPos - origin).normalized;
        float dist = Vector3.Distance(origin, targetPos);

        if (Physics.Raycast(origin, dir, dist, obstacleMask))
            return false; 

        return true;
    }


    private void UpdatePatrol()
    {
        if (playerVisible)
        {
            EnterState(State.Chase);
            return;
        }

        if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

       
        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            currentWaypoint = (currentWaypoint + 1) % patrolWaypoints.Length;
            agent.SetDestination(patrolWaypoints[currentWaypoint].position);
        }
    }

    

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

   

    private void UpdateAttack()
    {
        if (!playerVisible)
        {
            EnterState(State.Patrol);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

       
        if (dist > attackRange * 1.2f) 
        {
            EnterState(State.Chase);
            return;
        }

        
        Vector3 lookDir = (player.transform.position - transform.position).normalized;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(lookDir), 360f * Time.deltaTime);

       
        if (attackTimer <= 0f)
        {
            player.TakeDamage(attackDamage);
            attackTimer = attackCooldown;
        
        }
    }

   
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

       
    }



   

    private void OnDrawGizmosSelected()
    {
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        
        Vector3 leftBound = Quaternion.Euler(0, -sightAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, sightAngle * 0.5f, 0) * transform.forward;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawRay(transform.position, leftBound * sightRange);
        Gizmos.DrawRay(transform.position, rightBound * sightRange);

        
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hearRange);

        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);


    }
}