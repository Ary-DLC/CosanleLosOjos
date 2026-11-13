using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// T14 - Vida del jugador (3 puntos, sin barra: el daño se ve con DamageEffects)
// Se agrega al objeto PlayerCapsule.
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 3;
    public float invulnerableTime = 2f;  // después de un golpe, 2 s sin recibir más daño
    public float regenInterval = 5f;     // escondida, recupera 1 punto cada tantos segundos
    public float restartDelay = 3f;      // segundos antes de reiniciar al morir

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

        // Regeneración: solo mientras está escondida en un locker
        if (hiding != null && hiding.IsHidden && Health < maxHealth)
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= regenInterval)
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

        Invoke(nameof(Restart), restartDelay);
    }

    void Restart()
    {
        // Vuelve a cargar la misma escena (debe estar en la lista de escenas del Build Profile)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnGUI()
    {
        if (!IsDead) return;

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
