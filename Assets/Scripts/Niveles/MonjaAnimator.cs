using UnityEngine;
using UnityEngine.AI;

// Niveles - Animaciones de la monja (el enemigo), sin tocar EnemyAI
// Va en la raíz del enemigo (junto a EnemyAI y NavMeshAgent).
//   Speed   = velocidad del NavMeshAgent -> idle (quieta), walk (patrulla), run (persecución)
//   Attack  = cuando la jugadora recibe un golpe y esta monja es la que está cerca
//   Stunned = aturdida (se tapa la cara) cuando se apaga una vela ritual
// Al correr se mueve de forma errática, con sacudidas.
public class MonjaAnimator : MonoBehaviour
{
    public Animator animator;          // el Animator del modelo (hijo). Si está vacío se busca solo
    public float attackRange = 2.5f;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int AttackId = Animator.StringToHash("Attack");
    static readonly int StunnedId = Animator.StringToHash("Stunned");

    private NavMeshAgent agent;
    private PlayerHealth health;
    private float lastHit = -100f;
    private bool hasStunned;
    private bool stunned;

    // Sacudidas al correr
    private float jerkTimer;
    private Quaternion jerkTarget = Quaternion.identity;
    private Quaternion baseRotation = Quaternion.identity;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;   // el NavMeshAgent es quien la mueve
            baseRotation = animator.transform.localRotation;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == StunnedId) hasStunned = true;
        }
        health = FindAnyObjectByType<PlayerHealth>();
        if (health != null) lastHit = health.LastHitTime;
    }

    // Lo usa RitualManager (Nivel 3)
    public void SetStunned(bool value)
    {
        if (stunned == value) return;
        stunned = value;
        if (animator == null) return;
        if (hasStunned) animator.SetBool(StunnedId, value);
        else animator.speed = value ? 0.2f : 1f;   // si el Animator no tiene el estado, casi se congela
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

        // Corriendo: sacudidas bruscas de cabeza y cuerpo, y a veces acelera la animación
        bool running = speed > 3f && !stunned;
        if (running)
        {
            jerkTimer -= Time.deltaTime;
            if (jerkTimer <= 0f)
            {
                jerkTimer = Random.Range(0.12f, 0.45f);
                jerkTarget = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-22f, 22f), Random.Range(-12f, 12f));
                if (!stunned) animator.speed = Random.Range(0.85f, 1.45f);
            }
            animator.transform.localRotation = Quaternion.Slerp(animator.transform.localRotation, baseRotation * jerkTarget, 25f * Time.deltaTime);
        }
        else
        {
            animator.transform.localRotation = Quaternion.Slerp(animator.transform.localRotation, baseRotation, 8f * Time.deltaTime);
            if (!stunned || hasStunned) animator.speed = 1f;
        }
    }
}
