using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// T14 - Vida del jugador (3 puntos, sin barra: el daño se ve con DamageEffects)
// T17 - Al morir reaparece en el último confesionario (si ya guardó)
// Se agrega al objeto PlayerCapsule.
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 3;
    public float invulnerableTime = 2f;  // después de un golpe, 2 s sin recibir más daño
    public float regenInterval = 5f;     // escondida, recupera 1 punto cada tantos segundos
    public float slowRegenInterval = 20f; // fuera del locker: 1 punto cada 20 s (más lento que las vendas)
    public float slowRegenDelay = 8f;     // ...pero solo si lleva 8 s sin recibir golpes
    public float restartDelay = 3f;      // segundos antes de reiniciar al morir
    public float deathCinematicTime = 3.2f;   // HorrorFX: la monja atrapa a la jugadora (antes de la pantalla de muerte)

    private float deathTime;

    public int Health { get; private set; }
    public bool IsDead { get; private set; }
    public float LastHitTime { get; private set; } = -100f; // lo usa DamageEffects

    private PlayerHiding hiding;
    private float regenTimer;

    void Start()
    {
        Health = maxHealth;
        hiding = GetComponent<PlayerHiding>();
    }

    void Update()
    {
        if (IsDead) return;

        // Regeneración: rápida escondida en un locker, lenta en cualquier otro lado
        // (lenta solo si lleva un rato sin que la golpeen)
        bool hidden = hiding != null && hiding.IsHidden;
        bool calm = Time.time - LastHitTime >= slowRegenDelay;
        if (Health < maxHealth && (hidden || calm))
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= (hidden ? regenInterval : slowRegenInterval))
            {
                regenTimer = 0f;
                Health++;
            }
        }
        else
        {
            regenTimer = 0f;
        }
    }

    // Lo llama el enemigo
    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        if (Time.time - LastHitTime < invulnerableTime) return; // todavía en la pausa tras el golpe

        Health -= amount;
        LastHitTime = Time.time;

        if (Health <= 0) Die();
    }

    // Lo usan las vendas. Regresa false si ya tiene la vida llena.
    public bool Heal(int amount)
    {
        if (IsDead || Health >= maxHealth) return false;
        Health = Mathf.Min(maxHealth, Health + amount);
        return true;
    }

    void Die()
    {
        IsDead = true;
        Health = 0;

        // Ya no se puede mover ni interactuar
        GetComponent<FirstPersonController>().enabled = false;
        GetComponent<PlayerInteraction>().enabled = false;

        deathTime = Time.time;
        Invoke(nameof(Restart), deathCinematicTime + restartDelay);
    }

    void Restart()
    {
        // T17: si ya se guardó en un confesionario, reaparece ahí
        SaveSystem save = GetComponent<SaveSystem>();
        if (save != null && save.HasSave)
        {
            save.Respawn();
            return;
        }

        // Si nunca guardó: vuelve a cargar la misma escena (debe estar en la lista de escenas del Build Profile)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Lo llama SaveSystem al reaparecer en un confesionario
    public void Revive()
    {
        IsDead = false;
        Health = maxHealth;
        LastHitTime = -100f;
        regenTimer = 0f;

        GetComponent<FirstPersonController>().enabled = true;
        GetComponent<PlayerInteraction>().enabled = true;
    }

    void OnGUI()
    {
        if (PauseMenu.IsPaused) return; // el menú de pausa va encima
        if (!IsDead) return;
        if (Time.time - deathTime < deathCinematicTime) return;   // primero la cinemática de la monja

        // Pantalla negra con texto
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 36;
        style.normal.textColor = new Color(0.8f, 0f, 0f);
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "TE COSIERON LOS OJOS", style);
    }
}
