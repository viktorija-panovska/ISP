using UnityEngine;



public struct WorldLocation
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public (int x, int z) ChunkIndex { get => (X / Chunk.Width, Z / Chunk.Width); }
    public (int x, int y, int z) BlockIndex { get => (X % Chunk.Width, Y, Z % Chunk.Width); }

    public WorldLocation(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}



public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance;

    // World Map
    public int MapSeed;
    public Material MapMaterial;

    // Units
    public GameObject unitPrefab;
    public GameController controller;


    private void Start()
    {
        Instance = this;
        new WorldMap(MapSeed, MapMaterial);

        SpawnUnit();
    }


    public void SpawnUnit()
    {
        Chunk chunk = WorldMap.Instance.GetChunkAtIndex(Random.Range(0, WorldMap.ChunkNumber), Random.Range(0, WorldMap.ChunkNumber));

        int x = Random.Range(0, Chunk.Width);
        int y;
        int z = Random.Range(0, Chunk.Width);

        for (y = Chunk.Height - 1; y >= 0; --y)
            if (chunk.GetBlockAtIndex(x, y, z) != BlockType.Air || y == 0)
                break;

        GameObject unit = Instantiate(unitPrefab, 
            new Vector3(chunk.Coordinates.x + x + 0.5f, chunk.Coordinates.y + y + 1.5f, chunk.Coordinates.z + z + 0.5f), 
            Quaternion.identity);

        controller.AddUnit(unit, new WorldLocation((int)chunk.Coordinates.x + x, (int)chunk.Coordinates.y + y, (int)chunk.Coordinates.z + z));
    }


    public Vector3 WorldLocationToCoordinates(WorldLocation worldLocation)
    {
        Chunk chunk = WorldMap.Instance.GetChunkAtIndex(worldLocation.ChunkIndex.x, worldLocation.ChunkIndex.z);

        return new Vector3(chunk.Coordinates.x + worldLocation.BlockIndex.x * BlockData.Width,
                           chunk.Coordinates.y + worldLocation.BlockIndex.y * BlockData.Height,
                           chunk.Coordinates.z + worldLocation.BlockIndex.z * BlockData.Width);
    }


    public WorldLocation CoordinatesToWorldLocation(Vector3 coordinates)
    {
        Chunk chunk = WorldMap.Instance.GetChunkAtCoordinates(coordinates);
        (int x, int y, int z) = chunk.GetBlockIndexFromCoordinates(coordinates);

        return new WorldLocation(chunk.Index.x * Chunk.Width + x, y, chunk.Index.z * Chunk.Width + z);
    }
}
