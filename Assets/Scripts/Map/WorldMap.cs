using UnityEngine;

public static class WorldMap
{
    private static GameObject gameObject;

    // Noise Map
    private static int seed;
    private const float scale = 0.5f;
    private const int octaves = 1;              // number of levels of detail
    private const float persistence = 0.5f;     // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
    private const float lacunarity = 2;         // how much detail is added or removed at each octave (adjusts frequency) - must be > 1
    private static Vector2[] offsets;


    // World Map
    public static Transform Transform { get => gameObject.transform; }
    public static Vector3 Position { get => Transform.position; }
    public const int ChunkNumber = 1;
    private readonly static Chunk[,] chunkMap = new Chunk[ChunkNumber, ChunkNumber];


    // Texture
    public static Material WorldMaterial { get; private set; }



    // Create Map

    public static void Create(int mapSeed, Material worldMaterial)
    {
        gameObject = new GameObject();
        gameObject.transform.SetParent(LevelGenerator.Instance.transform);
        gameObject.name = "World Map";

        seed = mapSeed;
        WorldMaterial = worldMaterial;

        offsets = NoiseGenerator.GenerateNoiseOffsets(mapSeed, octaves);
        GenerateWorldMap();
    }

    private static void GenerateWorldMap()
    {
        for (int x = 0; x < ChunkNumber; ++x)
            for (int z = 0; z < ChunkNumber; ++z)
                chunkMap[x, z] = new Chunk((x, z));
    }

}
