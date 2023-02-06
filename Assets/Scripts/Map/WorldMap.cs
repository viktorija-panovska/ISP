using UnityEngine;

public static class WorldMap
{
    private static GameObject gameObject;

    // World Map
    public static Transform Transform { get => gameObject.transform; }
    public static Vector3 Position { get => Transform.position; }
    public const int ChunkNumber = 2;
    public const int Width = ChunkNumber * Chunk.Width;

    private readonly static Chunk[,] chunkMap = new Chunk[ChunkNumber, ChunkNumber];

    // Texture
    public static Material WorldMaterial { get; private set; }



    public static float GetVertexHeight((int x, int z) chunk, (float x, float z) localVertexCoord)
        => chunkMap[chunk.z, chunk.x].GetVertexHeight(localVertexCoord.x, localVertexCoord.z);

    public static float GetVertexHeight(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (float local_x, float local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_x, chunk_z].GetVertexHeight(local_x, local_z);
    }

    private static (int, int) GetChunkIndex(float x, float z)
    {
        int chunk_x = Mathf.FloorToInt(x / Chunk.Width);
        int chunk_z = Mathf.FloorToInt(z / Chunk.Width);

        if (chunk_x == ChunkNumber) chunk_x--;
        if (chunk_z == ChunkNumber) chunk_z--;

        return (chunk_x, chunk_z);
    }

    private static (float, float) LocalCoordsFromGlobal(float global_x, float global_z)
    {
        float local_x = global_x % Chunk.Width;
        float local_z = global_z % Chunk.Width;

        if (global_x == Width) local_x = Chunk.Width;
        if (global_z == Width) local_z = Chunk.Width;

        return (local_x, local_z);
    }


    // Create Map
    public static void Create(int mapSeed, Material worldMaterial)
        {
            gameObject = new GameObject();
            gameObject.transform.SetParent(LevelGenerator.Instance.transform);
            gameObject.name = "World Map";
            WorldMaterial = worldMaterial;

            NoiseGenerator.Initialize(mapSeed);

            GenerateWorldMap();
        }

    private static void GenerateWorldMap()
    {
        for (int z = 0; z < ChunkNumber; ++z)
            for (int x = 0; x < ChunkNumber; ++x)
                chunkMap[z, x] = new Chunk((x, z));
    }


    // Update Map
    public static void UpdateMap(WorldLocation location, bool decrease)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(location.X, location.Z);
        Chunk chunk = chunkMap[chunk_z, chunk_x];

        (float x, float z) local = LocalCoordsFromGlobal(location.X, location.Z);

        chunk.UpdateChunk(local, decrease);

        if (local.x == 0 || local.z == 0 || local.x == Chunk.Width || local.z == Chunk.Width)
            UpdateSurroundingChunks(chunk.ChunkIndex.x, chunk.ChunkIndex.z, local.x, local.z, decrease);
    }

    public static void UpdateSurroundingChunks(int chunk_X, int chunk_Z, float x, float z, bool decrease)
    {
        if (x == 0 && chunk_X > 0)
            chunkMap[chunk_Z, chunk_X - 1].UpdateChunk((Chunk.Width, z), decrease);

        if (z == 0 && chunk_Z > 0)
            chunkMap[chunk_Z - 1, chunk_X].UpdateChunk((x, Chunk.Width), decrease);

        if (x == Chunk.Width && chunk_X + 1 < ChunkNumber)
            chunkMap[chunk_Z, chunk_X + 1].UpdateChunk((0, z), decrease);

        if (z == Chunk.Width && chunk_Z + 1 < ChunkNumber)
            chunkMap[chunk_Z + 1, chunk_X].UpdateChunk((x, 0), decrease);


        // Corners

        if (x == 0 && z == 0 && chunk_X > 0 && chunk_Z > 0)
            chunkMap[chunk_Z - 1, chunk_X - 1].UpdateChunk((Chunk.Width, Chunk.Width), decrease);

        if (x == 0 && z == Chunk.Width && chunk_X > 0 && chunk_Z + 1 < ChunkNumber)
            chunkMap[chunk_Z + 1, chunk_X - 1].UpdateChunk((Chunk.Width, 0), decrease);

        if (x == Chunk.Width && z == 0 && chunk_Z > 0 && chunk_X + 1 < ChunkNumber)
            chunkMap[chunk_Z - 1, chunk_X + 1].UpdateChunk((0, Chunk.Width), decrease);

        if (x == Chunk.Width && z == Chunk.Width &&
            chunk_X + 1 < ChunkNumber && chunk_Z + 1 < ChunkNumber)
            chunkMap[chunk_Z + 1, chunk_X + 1].UpdateChunk((0, 0), decrease);
    }
}
