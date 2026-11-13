using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// T11 - Enemigo con NavMeshAgent
// Se agrega al enemigo (necesita NavMeshAgent y que la escena tenga NavMesh horneado).
// Patrulla -> si oye algo va a Investigar -> si te ve te Persigue -> si te pierde te Busca -> vuelve a Patrullar
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrullar, Investigar, Perseguir, Buscar }

    // Lista de todos los enemigos. La usa NoiseSystem para avisarles de los ruidos.
    public static List<EnemyAI> All = new List<EnemyAI>();

    [Header("Patrulla")]
    public Transform[] patrolPoints;    // puntos por los que camina en orden
    public float patrolSpeed = 1.8f;
    public float waitAtPoint = 2f;      // segundos que se queda en cada punto

    [Header("Vista")]
    public float viewDistance = 15f;    // qué tan lejos ve
    public float viewAngle = 110f;      // qué tan abierto ve (en grados)
    public float eyeHeight = 1.6f;      // altura de sus ojos
    public float feelDistance = 1.5f;   // tan cerca te "siente" aunque estés detrás

    [Header("Oído")]
    public float hearing = 1f;          // 1 = normal, 2 = oye el doble de lejos
    public float investigateSpeed = 2.8f;
    public float investigateTime = 5f;  // cuánto se queda donde oyó el ruido
    public float grabDistance = 2f;     // si llega a un locker y te oye respirar, te saca

    [Header("Persecución y ataque")]
    public float chaseSpeed = 5f;
    public float attackRange = 1.6f;
    public int damage = 2;
    public float pauseAfterHit = 2f;    // se queda quieto después de golpear

    [Header("Búsqueda")]
    public float searchTime = 10f;      // cuánto tiempo busca donde te perdió
    public float searchRadius = 5f;     // qué tan lejos de ahí busca

    public State state = State.Patrullar;

    private NavMeshAgent agent;
    private Transform player;
    private Transform playerHead;
    private PlayerHiding hiding;
    private PlayerHealth playerHealth;

    private int currentPoint;
    private float timer;           // tiempo que le queda investigando o buscando
    private float waitTimer;       // tiempo que lleva quieto en un punto
    private float pauseTimer;      // pausa después de golpear
    private Vector3 targetPosition;   // donde oyó el ruido o donde te vio por última vez
    private bool seesPlayer;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        player = p.transform;
        playerHead = Camera.main.transform;
        hiding = p.GetComponent<PlayerHiding>();
        playerHealth = p.GetComponent<PlayerHealth>();

        StartPatrol();
    }

    void Update()
    {
        // Si no está parado sobre el NavMesh, intentamos ponerlo encima y esperamos
        if (!agent.isOnNavMesh)
        {
            TryPlaceOnNavMesh();
            return;
        }

        // Si la jugadora murió, ya no hace nada
        if (playerHealth != null && playerHealth.IsDead)
        {
            agent.isStopped = true;
            return;
        }

        // Pausa después de dar un golpe
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f) agent.isStopped = false;
            return;
        }

        OpenDoorsInFront();

        // Si te ve, deja todo y te persigue
        seesPlayer = CanSeePlayer();
        if (seesPlayer)
        {
            targetPosition = player.position;
            if (state != State.Perseguir) StartChase();
        }

        switch (state)
        {
            case State.Patrullar:  Patrol();      break;
            case State.Investigar: Investigate(); break;
            case State.Perseguir:  Chase();       break;
            case State.Buscar:     Search();      break;
        }
    }

    // ---------- Estados ----------

    void StartPatrol()
    {
        state = State.Patrullar;
        agent.speed = patrolSpeed;
        if (patrolPoints.Length > 0)
            GoTo(patrolPoints[currentPoint].position);
    }

    void Patrol()
    {
        if (patrolPoints.Length == 0 || !Arrived()) return;

        // Llegó al punto: espera un rato y se va al siguiente
        waitTimer += Time.deltaTime;
        if (waitTimer >= waitAtPoint)
        {
            waitTimer = 0f;
            currentPoint = (currentPoint + 1) % patrolPoints.Length;
            GoTo(patrolPoints[currentPoint].position);
        }
    }

    // Lo llama NoiseSystem cuando algo hace ruido
    public void HearNoise(Vector3 position, float radius)
    {
        if (agent == null) return;            // todavía no empieza el juego
        if (state == State.Perseguir) return; // si ya te persigue, ignora los ruidos

        float distance = Vector3.Distance(transform.position, position);
        if (distance > radius * hearing) return; // muy lejos, no lo oyó

        state = State.Investigar;
        timer = investigateTime;
        targetPosition = position;
        agent.speed = investigateSpeed;
        GoTo(position);
    }

    void Investigate()
    {
        if (!Arrived()) return;

        // Si estás escondida aquí cerca y no aguantas la respiración, te oye y te saca
        if (hiding != null && hiding.IsHidden && !hiding.IsHoldingBreath &&
            Vector3.Distance(transform.position, player.position) < grabDistance)
        {
            hiding.ForceExit();
            StartChase();
            return;
        }

        // Mira a su alrededor un rato y regresa a patrullar
        LookAround();
        timer -= Time.deltaTime;
        if (timer <= 0f) StartPatrol();
    }

    void StartChase()
    {
        state = State.Perseguir;
        agent.speed = chaseSpeed;
    }

    void Chase()
    {
        GoTo(targetPosition);

        if (!seesPlayer)
        {
            // Te perdió: va a buscar al último lugar donde te vio
            StartSearch();
            return;
        }

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
            Attack();
    }

    void Attack()
    {
        playerHealth.TakeDamage(damage);

        // Se queda quieto un momento para darte chance de huir
        pauseTimer = pauseAfterHit;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    void StartSearch()
    {
        state = State.Buscar;
        timer = searchTime;
        waitTimer = 0f;
        agent.speed = investigateSpeed;
        GoTo(targetPosition);
    }

    void Search()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            StartPatrol();
            return;
        }

        if (!Arrived()) return;

        // Llegó: mira alrededor y luego va a otro punto cercano al azar
        LookAround();
        waitTimer += Time.deltaTime;
        if (waitTimer >= 1.5f)
        {
            waitTimer = 0f;
            Vector3 random = targetPosition + Random.insideUnitSphere * searchRadius;
            if (NavMesh.SamplePosition(random, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
                GoTo(hit.position);
        }
    }

    // ---------- Ayudantes ----------

    bool CanSeePlayer()
    {
        if (hiding != null && hiding.IsHidden) return false; // escondida = invisible

        Vector3 eyes = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = playerHead.position - eyes;
        float distance = toPlayer.magnitude;

        if (distance > viewDistance) return false;

        // ¿Está dentro de su cono de visión? (si está muy cerca la siente igual)
        if (Vector3.Angle(transform.forward, toPlayer) > viewAngle / 2f && distance > feelDistance)
            return false;

        // ¿Hay una pared en medio?
        if (Physics.Raycast(eyes, toPlayer.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform.IsChildOf(player);

        return true;
    }

    bool Arrived()
    {
        if (!agent.isOnNavMesh) return false;
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f;
    }

    // Igual que agent.SetDestination, pero solo si está sobre el NavMesh (si no, da error)
    void GoTo(Vector3 position)
    {
        if (agent.isOnNavMesh)
            agent.SetDestination(position);
    }

    // Busca el punto del NavMesh más cercano (hasta 3 m) y pone ahí al enemigo
    private bool warnedNoNavMesh;
    void TryPlaceOnNavMesh()
    {
        if (!agent.enabled) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            StartPatrol(); // ya está en el NavMesh, empieza a patrullar
        }
        else if (!warnedNoNavMesh)
        {
            warnedNoNavMesh = true;
            Debug.LogWarning("El enemigo '" + name + "' no está cerca de ningún NavMesh. ¿Ya horneaste el NavMesh (NavMeshSurface > Bake)?", this);
        }
    }

    void LookAround()
    {
        transform.Rotate(0f, 90f * Time.deltaTime, 0f);
    }

    // Si tiene una puerta enfrente, la abre
    void OpenDoorsInFront()
    {
        Vector3 chest = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(chest, transform.forward, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            Door door = hit.collider.GetComponentInParent<Door>();
            if (door != null) door.OpenByEnemy();
        }
    }

    // Dibuja en la vista Scene qué tan lejos ve
    void OnDrawGizmosSelected()
    {
        Vector3 eyes = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Vector3 left = Quaternion.Euler(0f, -viewAngle / 2f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle / 2f, 0f) * transform.forward;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(eyes, left * viewDistance);
        Gizmos.DrawRay(eyes, right * viewDistance);
    }
}
