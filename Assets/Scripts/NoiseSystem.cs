using UnityEngine;

// T12 - Sistema de ruido
// No se agrega a ningún objeto. Cualquier script puede llamar:
//     NoiseSystem.MakeNoise(posicion, radio, "qué hizo el ruido");
// y todos los enemigos que estén dentro del radio lo escuchan.
public static class NoiseSystem
{
    public static bool showLogs = true;   // ponlo en false para quitar los mensajes de la Console

    public static void MakeNoise(Vector3 position, float radius, string source = "ruido")
    {
        // Mensaje en la Console para comprobar dónde salió cada ruido
        if (showLogs)
            Debug.Log("[Ruido] " + source + " en " + position + " (radio " + radius + ")");

        // Avisamos a cada enemigo; cada uno decide si lo alcanzó a oír
        foreach (EnemyAI enemy in EnemyAI.All)
            enemy.HearNoise(position, radius);

        // Línea amarilla en la vista Scene para ver dónde y qué tan fuerte fue el ruido
        Debug.DrawRay(position, Vector3.up * radius, Color.yellow, 1f);
    }
}
