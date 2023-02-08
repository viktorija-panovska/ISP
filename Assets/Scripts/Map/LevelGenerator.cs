using UnityEngine;


public struct WorldLocation
{
    public float X { get; }
    public float Z { get; }

    public WorldLocation(float x, float z)
    {
        X = Mathf.Round(x / Chunk.TileWidth) * Chunk.TileWidth;
        Z = Mathf.Round(z / Chunk.TileWidth) * Chunk.TileWidth;
    }
}


public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance;

    public Transform Viewer;

    // World Map
    public int MapSeed;
    public Material MapMaterial;

    // Units
    public GameObject unitPrefab;
    public GameController controller;


    private void Start()
    {
        Instance = this;
        WorldMap.Create(MapSeed, MapMaterial);

        //for (int i = 0; i < 3; ++i)
        //    SpawnUnit();
    }

    private void Update()
    {
        WorldMap.DrawMap(Viewer.position);
    }


    private void SpawnUnit()
    {
        WorldLocation location = GenerateRandomWorldLocation();

        // TODO: Change from hard coded offsets to offsets based on the height of the prefab
        GameObject unit = Instantiate(
            unitPrefab, 
            new Vector3(location.X, 
                WorldMap.GetVertexHeight(location) + 15, 
                location.Z),
            Quaternion.identity
        );

        controller.AddUnit(unit, location);
    }


    private WorldLocation GenerateRandomWorldLocation()
    {
        int x = Random.Range(0, WorldMap.Width);
        int z = Random.Range(0, WorldMap.Width);

        return new WorldLocation(x, z);
    }
}
