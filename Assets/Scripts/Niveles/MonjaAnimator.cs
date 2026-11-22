using UnityEngine;
using UnityEngine.AI;

// Niveles - Animaciones de la monja (el enemigo), sin tocar EnemyAI
// Va en la raíz del enemigo (junto a EnemyAI y NavMeshAgent).
//   Speed  = velocidad del NavMeshAgent -> idle (quieta), walk (patrulla), run (persecución)
//   Attack = cuando la jugadora recibe un golpe y esta monja es la que está cerca
public class MonjaAnimator : MonoBehaviour
{
    public Animator animator;          // el Animator del modelo (hijo). Si está vacío se busca solo
    public float attackRange = 2.5f;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int AttackId = Animator.StringToHash("Attack");

    private NavMeshAgent agent;
    private PlayerHealth health;
    private float lastHit = -100f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;   // el NavMeshAgent es quien la mueve
        health = FindAnyObjectByType<PlayerHealth>();
        if (health != null) lastHit = health.LastHitTime;
    }

    void Update()
    {
        if (animator == null) return;

        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        animator.SetFloat(SpeedId, speed, 0.15f, Time.deltaTime);

        // ¿Golpeó a la jugadora? (EnemyAI no avisa: lo vemos en PlayerHealth)
        if (health != null && health.LastHitTime != lastHit)
        {
            if (health.LastHitTime > lastHit && Vector3.Distance(transform.position, health.transform.position) <= attackRange)
                animator.SetTrigger(AttackId);
            lastHit = health.LastHitTime;
        }
    }
}
