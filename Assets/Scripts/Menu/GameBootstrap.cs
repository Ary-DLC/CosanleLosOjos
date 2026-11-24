using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// Menú - Arranque automático
// No se agrega a ningún objeto: Unity llama a Init() solo al iniciar el juego.
// En cualquier escena que tenga al jugador (FirstPersonController) crea por código
// el menú de pausa y el objeto que aplica los ajustes. Así no hay que tocar la escena del juego.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        GameSettings.Load();
        // (GameInput se prepara solo la primera vez que se usa, cuando el Input System ya arrancó)
        GameInput.GameplayBlocked = false;
        GameSettings.ApplyDisplay();

        SceneManager.sceneLoaded -= OnSceneLoaded; // por si acaso no se suscribe dos veces
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.GameplayBlocked = false;
            PauseMenu.IsPaused = false;
        }

        // ¿Es una escena de juego? (tiene al jugador)
        if (Object.FindAnyObjectByType<FirstPersonController>() == null) return;
        if (Object.FindAnyObjectByType<SettingsApplier>() != null) return;

        GameObject go = new GameObject("Sistemas del juego (pausa y ajustes)");
        go.AddComponent<SettingsApplier>();
        go.AddComponent<PauseMenu>();
        go.AddComponent<ObjectiveHUD>();   // objetivo y aviso de curación (arriba a la izquierda)
        go.AddComponent<HorrorFX>();       // persecución, golpes y muerte

        // Nivel 3: las velas rituales, la barrera y las monjas
        if (scene.name.Contains("Umbral")) RitualManager.Build();
    }
}
