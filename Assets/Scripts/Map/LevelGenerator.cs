using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance;

    // World Map
    public int MapSeed;
    public Material MapMaterial;


    private void Start()
    {
        Instance = this;
        WorldMap.Create(MapSeed, MapMaterial);
    }
}
