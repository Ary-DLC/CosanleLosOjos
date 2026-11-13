using UnityEngine;

// T12 - Sistema de ruido
// No se agrega a ningún objeto. Cualquier script puede llamar:
//     NoiseSystem.MakeNoise(posicion, radio);
// y todos los enemigos que estén dentro del radio lo escuchan.
public static class NoiseSystem
{
    public static void MakeNoise(Vector3 position, float radius)
    {
        // Avisamos a cada enemigo; cada uno decide si lo alcanzó a oír
        foreach (EnemyAI enemy in EnemyAI.All)
            enemy.HearNoise(position, radius);

        // Línea amarilla en la vista Scene para ver dónde y qué tan fuerte fue el ruido
        Debug.DrawRay(position, Vector3.up * radius, Color.yellow, 1f);
    }
}
