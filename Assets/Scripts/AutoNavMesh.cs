using UnityEngine;
using Unity.AI.Navigation;

// Piso y NavMesh automáticos
// NO hace falta ponerlo a mano: EnemyAI lo crea solo si no existe en la escena.
// Al iniciar el juego agranda el piso ("Plane") y hornea su NavMesh, antes de que el enemigo se mueva.
public class AutoNavMesh : MonoBehaviour
{
    public string floorName = "Plane";                       // nombre del objeto piso
    public Vector3 floorScale = new Vector3(5f, 1f, 5f);     // 5 x 5 = un piso de 50 x 50 metros

    public bool IsBuilt { get; private set; }

    void Awake()
    {
        Build();
    }

    // Lo llama EnemyAI en su Awake. Si no hay un AutoNavMesh en la escena, lo crea.
    public static void Ensure()
    {
        if (FindAnyObjectByType<AutoNavMesh>() == null)
            new GameObject("AutoNavMesh").AddComponent<AutoNavMesh>(); // AddComponent llama a Awake al momento
    }

    public void Build()
    {
        if (IsBuilt) return;

        GameObject floor = GameObject.Find(floorName);
        if (floor == null)
        {
            Debug.LogWarning("AutoNavMesh: no encontré ningún objeto llamado '" + floorName + "'.", this);
            return;
        }

        // 1. Agrandar el piso
        floor.transform.localScale = floorScale;

        // 2. Conseguir su NavMeshSurface (si no tiene, se la ponemos)
        NavMeshSurface surface = floor.GetComponent<NavMeshSurface>();
        if (surface == null) surface = floor.AddComponent<NavMeshSurface>();

        // 3. Que la jugadora no haga un "hoyo" en el NavMesh donde está parada
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.GetComponent<NavMeshModifier>() == null)
        {
            NavMeshModifier modifier = player.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
        }

        // 4. Hornear el NavMesh (el enemigo se ignora solo: NavMeshSurface no cuenta objetos con NavMeshAgent)
        surface.BuildNavMesh();
        IsBuilt = true;
        Debug.Log("AutoNavMesh: NavMesh horneado sobre '" + floor.name + "' con escala " + floorScale);
    }
}
