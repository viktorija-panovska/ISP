using UnityEngine;



public struct WorldLocation
{
    public Chunk Chunk;
    public int X;
    public int Y;
    public int Z;

    public WorldLocation(Chunk chunk, int x, int y, int z)
    {
        Chunk = chunk;
        X = x;
        Y = y;
        Z = z;
    }
}



public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance;

    // World Map
    public WorldMap WorldMap;
    public int MapSeed;
    public Material MapMaterial;

    // Units
    public GameObject unitPrefab;
    public GameController controller;


    private void Start()
    {
        Instance = this;
        WorldMap = new WorldMap(MapSeed, MapMaterial);

        SpawnUnit();
    }


    public void SpawnUnit()
    {
        Chunk chunk = WorldMap.GetChunkAtIndex(Random.Range(0, WorldMap.ChunkNumber), Random.Range(0, WorldMap.ChunkNumber));

        int x = Random.Range(0, Chunk.Width);
        int y;
        int z = Random.Range(0, Chunk.Width);

        for (y = Chunk.Height - 1; y >= 0; --y)
            if (chunk.GetBlockAtIndex(x, y, z) != BlockType.Air || y == 0)
                break;

        GameObject unit = Instantiate(unitPrefab, 
            new Vector3(chunk.Coordinates.x + x + 0.5f, chunk.Coordinates.y + y + 1.5f, chunk.Coordinates.z + z + 0.5f), 
            Quaternion.identity);

        controller.AddUnit(unit, new WorldLocation(chunk, x, y, z));
    }


    public Vector3 WorldLocationToCoordinates(WorldLocation worldLoc)
        => new Vector3(worldLoc.Chunk.Coordinates.x + worldLoc.X * BlockData.Width,
                       worldLoc.Chunk.Coordinates.y + worldLoc.Y * BlockData.Height,
                       worldLoc.Chunk.Coordinates.z + worldLoc.Z * BlockData.Width);


    public WorldLocation CoordinatesToWorldLocation(Vector3 coordinates)
    {
        Chunk chunk = Instance.WorldMap.GetChunkAtCoordinates(coordinates);
        (int x, int y, int z) = chunk.GetBlockIndexFromCoordinates(coordinates);

        return new WorldLocation(chunk, x, y, z);
    }
}
