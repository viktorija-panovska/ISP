using UnityEngine;

public static class WorldMap
{
    private static GameObject gameObject;

    // World Map
    public static Transform Transform { get => gameObject.transform; }
    public static Vector3 Position { get => Transform.position; }
    public const int ChunkNumber = 2;
    private readonly static Chunk[,] chunkMap = new Chunk[ChunkNumber, ChunkNumber];

    // Texture
    public static Material WorldMaterial { get; private set; }


    public static float GetVertexHeight((int x, int z) chunk, (int x, int z) vertex) => chunkMap[chunk.x, chunk.z].GetVertexHeight(vertex.x, vertex.z);


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
        for (int x = 0; x < ChunkNumber; ++x)
            for (int z = 0; z < ChunkNumber; ++z)
                chunkMap[x, z] = new Chunk((x, z));
    }


    // Update Map

    public static void UpdateMap(Vector3 clickPosition, bool decrease)
    {
        // find chunk
        int chunk_X = Mathf.FloorToInt(clickPosition.x / Chunk.WidthInPixels);
        int chunk_Z = Mathf.FloorToInt(clickPosition.z / Chunk.WidthInPixels);
        Chunk chunk = chunkMap[chunk_X, chunk_Z];

        // find vertex
        int x = (int)Mathf.Round(clickPosition.x / Chunk.TileWidth % Chunk.TileNumber);
        int z = (int)Mathf.Round(clickPosition.z / Chunk.TileWidth % Chunk.TileNumber);

        chunk.UpdateChunk(x, z, decrease);

        UpdateSurroundingChunks(chunk_X, chunk_Z, x, z, decrease);
    }

    private static void UpdateSurroundingChunks(int chunk_X, int chunk_Z, int x, int z, bool decrease)
    {
        if (x == 0)
        {
            if (z == 0)
            {
                if (chunk_X > 0)
                    chunkMap[chunk_X - 1, chunk_Z].UpdateChunk(Chunk.MaxVertexIndex, 0, decrease);
                else if (chunk_Z > 0)
                    chunkMap[chunk_X, chunk_Z - 1].UpdateChunk(0, Chunk.MaxVertexIndex, decrease);
                else if (chunk_X > 0 && chunk_Z > 0)
                    chunkMap[chunk_X - 1, chunk_Z - 1].UpdateChunk(Chunk.MaxVertexIndex, Chunk.MaxVertexIndex, decrease);
            }
            else if (z == Chunk.MaxVertexIndex)
            {
                if (chunk_X > 0)
                    chunkMap[chunk_X - 1, chunk_Z].UpdateChunk(Chunk.MaxVertexIndex, Chunk.MaxVertexIndex, decrease);
                else if (chunk_Z + 1 < ChunkNumber)
                    chunkMap[chunk_X, chunk_Z + 1].UpdateChunk(0, 0, decrease);
                else if (chunk_X > 0 && chunk_Z + 1 < ChunkNumber)
                    chunkMap[chunk_X - 1, chunk_Z + 1].UpdateChunk(Chunk.MaxVertexIndex, 0, decrease);
            }
            else
            {
                if (chunk_X > 0)
                    chunkMap[chunk_X - 1, chunk_Z].UpdateChunk(Chunk.MaxVertexIndex, z, decrease);
            }
        }

        else if (z == 0)
        {
            if (x == Chunk.MaxVertexIndex)
            {
                if (chunk_Z > 0)
                    chunkMap[chunk_X, chunk_Z - 1].UpdateChunk(0, Chunk.MaxVertexIndex, decrease);
                else if (chunk_X + 1 < ChunkNumber)
                    chunkMap[chunk_X + 1, chunk_Z].UpdateChunk(0, 0, decrease);
                else if (chunk_Z > 0 && chunk_X + 1 < ChunkNumber)
                    chunkMap[chunk_X + 1, chunk_Z - 1].UpdateChunk(0, Chunk.MaxVertexIndex, decrease);
            }
            else
            {
                if (chunk_Z > 0)
                    chunkMap[chunk_X, chunk_Z - 1].UpdateChunk(x, Chunk.MaxVertexIndex, decrease);
            }
        }

        else if (x == Chunk.MaxVertexIndex && z == Chunk.MaxVertexIndex)
        {
            if (chunk_X + 1 < ChunkNumber)
                chunkMap[chunk_X + 1, chunk_Z].UpdateChunk(0, Chunk.MaxVertexIndex, decrease);
            else if (chunk_Z + 1 < ChunkNumber)
                chunkMap[chunk_X, chunk_Z + 1].UpdateChunk(Chunk.MaxVertexIndex, 0, decrease);
            else if (chunk_X - 1 < ChunkNumber && chunk_Z - 1 < ChunkNumber)
                chunkMap[chunk_X + 1, chunk_Z + 1].UpdateChunk(0, 0, decrease);
        }
    }
}
