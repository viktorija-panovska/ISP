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

    public GameObject CameraRig;

    // World Map
    public int MapSeed;
    public Material MapMaterial;

    // Units
    public GameObject UnitPrefab;
    public GameController Controller;


    private void Start()
    {
        Instance = this;
        WorldMap.Create(MapSeed, MapMaterial);
        CameraRig.transform.position = new Vector3(WorldMap.Width / 2, CameraRig.transform.position.y, WorldMap.Width / 2);

        WorldMap.DrawMap(CameraRig.transform.position);
        //for (int i = 0; i < 3; ++i)
        //    SpawnUnit();
    }


    private void Update()
    {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, 0, Screen.height / 2)), out RaycastHit hitInfo, Mathf.Infinity))
            WorldMap.DrawMap(hitInfo.point);
    }


    private void SpawnUnit()
    {
        WorldLocation location = GenerateRandomWorldLocation();

        // TODO: Change from hard coded offsets to offsets based on the height of the prefab
        GameObject unit = Instantiate(
            UnitPrefab, 
            new Vector3(location.X, 
                WorldMap.GetVertexHeight(location) + 15, 
                location.Z),
            Quaternion.identity
        );

        Controller.AddUnit(unit, location);
    }


    private WorldLocation GenerateRandomWorldLocation()
    {
        int x = Random.Range(0, WorldMap.Width);
        int z = Random.Range(0, WorldMap.Width);

        return new WorldLocation(x, z);
    }
}
