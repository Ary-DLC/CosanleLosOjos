using UnityEngine;
using StarterAssets;

// T12 - Ruido de los pasos del jugador
// Se agrega al objeto PlayerCapsule.
// Correr hace mucho ruido, caminar poco y agachada no hace ruido.
public class PlayerNoise : MonoBehaviour
{
    public float runNoise = 12f;      // radio del ruido al correr
    public float walkNoise = 3f;      // radio del ruido al caminar (0 = nada)
    public float stepInterval = 0.4f; // cada cuánto se "da un paso"

    private StarterAssetsInputs input;
    private FirstPersonController fpc;
    private PlayerStaminaCrouch stamina;
    private float stepTimer;

    void Start()
    {
        // Buscamos en este objeto o en el padre (por si el script quedó en un hijo)
        input = GetComponentInParent<StarterAssetsInputs>();
        fpc = GetComponentInParent<FirstPersonController>();
        stamina = fpc.GetComponent<PlayerStaminaCrouch>();
    }

    void Update()
    {
        // Si no se mueve, está en el aire o está escondida, no hay pasos.
        // Mientras la botella va volando tampoco, para que el enemigo solo oiga el choque.
        bool moving = input.move != Vector2.zero && fpc.Grounded && fpc.enabled;
        if (!moving || Throwable.InFlight)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer += Time.deltaTime;
        if (stepTimer < stepInterval) return;
        stepTimer = 0f;

        bool running = input.sprint && !stamina.IsCrouching && !stamina.IsExhausted;

        if (running)
            NoiseSystem.MakeNoise(fpc.transform.position, runNoise, "pasos corriendo");
        else if (!stamina.IsCrouching && walkNoise > 0f)
            NoiseSystem.MakeNoise(fpc.transform.position, walkNoise, "pasos caminando");
    }
}
