using System.Collections;
using UnityEngine;

// Niveles - El enemigo no detecta a la jugadora durante los primeros segundos del nivel
// Se agrega al enemigo junto con EnemyAI (no cambia EnemyAI).
// Apaga EnemyAI un rato: no ve, no oye y no ataca. El NavMeshAgent sigue caminando
// hacia su primer punto de patrulla, así no se queda congelado.
[RequireComponent(typeof(EnemyAI))]
public class EnemyStartDelay : MonoBehaviour
{
    public float delay = 10f;   // segundos sin detectar al empezar

    IEnumerator Start()
    {
        EnemyAI ai = GetComponent<EnemyAI>();

        // Esperamos un cuadro: que EnemyAI y SoundManager ya hayan hecho su Start
        // (SoundManager registra los sonidos de los enemigos al iniciar)
        yield return null;

        ai.enabled = false;
        yield return new WaitForSeconds(delay - Time.deltaTime);
        ai.enabled = true;
    }
}
