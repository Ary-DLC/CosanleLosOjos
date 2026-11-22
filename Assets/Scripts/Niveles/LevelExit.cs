using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// Niveles - Salida del nivel (escalera, pozo o el centro del círculo)
// Se agrega a un objeto con un BoxCollider marcado como Trigger.
// Al entrar la jugadora: fundido a negro, guarda y carga el siguiente nivel.
// Si "isEnding" está marcado: fundido a negro, texto "Fin" y regresa al menú principal.
[RequireComponent(typeof(BoxCollider))]
public class LevelExit : MonoBehaviour
{
    public string nextScene = "";          // escena del siguiente nivel
    public bool isEnding = false;          // true = último nivel (Fin)
    public string text = "";               // texto sobre el negro (ej. "Las Catacumbas" o "Fin")
    public string menuScene = "MainMenu";
    public float fadeTime = 1.5f;

    private bool triggered;
    private float fade;                    // 0 = nada, 1 = negro
    private float textAlpha;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player")) return;
        triggered = true;
        StartCoroutine(Go(other.gameObject));
    }

    IEnumerator Go(GameObject player)
    {
        // Ya no se puede mover ni usar nada
        FirstPersonController fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;
        GameInput.GameplayBlocked = true;

        // Fundido a negro
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            fade = t / fadeTime;
            yield return null;
        }
        fade = 1f;

        // Texto sobre el negro
        if (text.Length > 0)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime) { textAlpha = t; yield return null; }
            textAlpha = 1f;
            yield return new WaitForSeconds(isEnding ? 3f : 1.5f);
        }

        if (isEnding)
        {
            SaveSystem.DeleteSavedGame();   // terminó el juego: "Continuar" ya no aplica
            SaveSystem.LoadOnStart = false;
            SceneManager.LoadScene(menuScene);
        }
        else
        {
            // Guardar al cambiar de nivel (pilas, vendas, batería y notas)
            SaveSystem.SaveLevelStart(nextScene, player.GetComponent<Inventory>(), player.GetComponent<NightVisionCamera>());
            SceneManager.LoadScene(nextScene);
        }
    }

    void OnGUI()
    {
        if (fade <= 0f) return;
        GUI.depth = -200; // encima de todo

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, fade);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        if (textAlpha > 0f)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = isEnding ? 64 : 36;
            style.normal.textColor = new Color(0.8f, 0.75f, 0.65f, textAlpha);
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text, style);
        }
    }
}
