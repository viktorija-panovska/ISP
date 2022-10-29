using UnityEngine;


public class WorldMap
{
    public readonly GameObject gameObject;

    // Noise Map
    private readonly int mapSeed;
    private const float scale = 0.5f;
    private const int octaves = 1;              // number of levels of detail
    private const float persistence = 0.5f;     // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
    private const float lacunarity = 2;         // how much detail is added or removed at each octave (adjusts frequency) - must be > 1
    private readonly Vector2[] offsets;


    // World Map
    public const int ChunkNumber = 1;
    public static int VoxelNumber { get => ChunkNumber * Chunk.Width; }
    private readonly Chunk[,] chunkMap = new Chunk[ChunkNumber, ChunkNumber];


    // Texture
    public readonly Material WorldMaterial;
    public const int TextureAtlasBlocks = 4;
    public static float NormalizedTextureBlockSize { get => 1f / (float)TextureAtlasBlocks; }
    public static readonly BlockProperties[] BlockTypes =
    {
        new BlockProperties(false, 0, 0),      // None
        new BlockProperties(false, 0, 0),      // Air
        new BlockProperties(true,  1, 1),      // Water
        new BlockProperties(true,  2, 3)       // Grass
    };



    public WorldMap(int mapSeed, Material worldMaterial)
    {
        gameObject = new GameObject();
        gameObject.transform.SetParent(LevelGenerator.Instance.transform);
        gameObject.name = "World Map";

        this.mapSeed = mapSeed;
        WorldMaterial = worldMaterial;

        offsets = NoiseGenerator.GenerateNoiseOffsets(mapSeed, octaves);
        GenerateWorldMap();
    }



    public Chunk GetChunkAtIndex(int x, int z) => chunkMap[x, z];

    public Chunk GetChunkAtCoordinates(Vector3 chunkPosition)
        => chunkMap[Mathf.FloorToInt(chunkPosition.x / Chunk.Width), Mathf.FloorToInt(chunkPosition.z / Chunk.Width)];

    public BlockType GetBlockTypeAtPosition(Vector3 blockPosition)
    {
        if (!IsBlockInWorld(blockPosition))
            return BlockType.Air;

        Chunk chunk = GetChunkAtCoordinates(blockPosition);

        if (chunk == null)
            return BlockType.Air;

        int x = Mathf.FloorToInt(blockPosition.x) - Mathf.FloorToInt(chunk.Coordinates.x);
        int y = Mathf.FloorToInt(blockPosition.y);
        int z = Mathf.FloorToInt(blockPosition.z) - Mathf.FloorToInt(chunk.Coordinates.z);

        BlockType block = chunk.GetBlockAtIndex(x, y, z);

        if (block == BlockType.None)
            return GenerateBlock(blockPosition);

        return block;
    }



    public void GenerateWorldMap()
    {
        for (int x = 0; x < ChunkNumber; ++x)
            for (int z = 0; z < ChunkNumber; ++z)
                chunkMap[x, z] = new Chunk(this, (x, z));
    }

    public BlockType GenerateBlock(Vector3 blockPosition)
    {
        float y = Mathf.FloorToInt(blockPosition.y);

        if (!IsBlockInWorld(blockPosition))
            return BlockType.Air;

        if (y == 0)
            return BlockType.Water;

        float noise = NoiseGenerator.GetPerlinAtPosition(new Vector2(blockPosition.x, blockPosition.z), scale, offsets, octaves, persistence, lacunarity);
        int terrainHeight = Mathf.FloorToInt(noise * Chunk.Height);

        if (y <= terrainHeight)
            return BlockType.Grass;
        else
            return BlockType.Air;
    }



    public bool IsChunkInWorld(Vector3 chunkPosition)
    {
        int x = Mathf.FloorToInt(chunkPosition.x / Chunk.Width);
        int z = Mathf.FloorToInt(chunkPosition.z / Chunk.Width);


        if (x < 0 || x >= ChunkNumber ||
            z < 0 || z >= ChunkNumber)
            return false;
        
        return true;
    }

    private bool IsBlockInWorld(Vector3 blockPosition)
    {
        if (blockPosition.x < 0 || blockPosition.x > VoxelNumber - 1 ||
            blockPosition.y < 0 || blockPosition.y > Chunk.Height - 1 ||
            blockPosition.z < 0 || blockPosition.z > VoxelNumber - 1)
            return false;

        return true;
    }
}
