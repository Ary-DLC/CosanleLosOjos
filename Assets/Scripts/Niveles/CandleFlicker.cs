using UnityEngine;

// Niveles - Parpadeo leve de la luz de una vela
// Se agrega a una Point Light. Cambia un poquito la intensidad con ruido suave (sin saltos).
[RequireComponent(typeof(Light))]
public class CandleFlicker : MonoBehaviour
{
    [Range(0f, 0.5f)] public float amount = 0.15f;   // cuánto varía (0.15 = ±15%)
    public float speed = 6f;                          // qué tan rápido

    private Light candle;
    private float baseIntensity;
    private float seed;

    void Start()
    {
        candle = GetComponent<Light>();
        baseIntensity = candle.intensity;
        seed = Random.value * 100f;   // cada vela parpadea distinto
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;   // -1 a 1
        candle.intensity = baseIntensity * (1f + n * amount);
    }
}
