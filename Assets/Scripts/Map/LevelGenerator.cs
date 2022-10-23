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
    // World Map
    public WorldMap WorldMap;
    public int MapSeed;
    public Material MapMaterial;

    // Units
    public GameObject unitPrefab;
    public GameController controller;


    private void Start()
    {
        WorldMap = new WorldMap(this, MapSeed, MapMaterial);

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

        Instantiate(unitPrefab, 
            new Vector3(chunk.PositionInGameWorld.x + x + 0.5f, chunk.PositionInGameWorld.y + y + 1.5f, chunk.PositionInGameWorld.z + z + 0.5f), 
            Quaternion.identity);

        controller.AddUnit(new WorldLocation(chunk, x, y, z));
    }
}
