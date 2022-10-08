using UnityEngine;


public class WorldMap : MonoBehaviour
{
    public const int ChunkNumber = 5;
    public static int VoxelNumber { get => ChunkNumber * Chunk.Width; }

    private Chunk[,] mapChunks = new Chunk[ChunkNumber, ChunkNumber];


    // Texture
    public Material WorldMaterial;
    public const int TextureAtlasBlocks = 4;
    public static float NormalizedTextureBlockSize { get => 1f / (float)TextureAtlasBlocks; }

    public static readonly BlockProperties[] BlockTypes =
    {
        new BlockProperties(false, 0),      // Air
        new BlockProperties(true,  1),      // Water
        new BlockProperties(true, 11),      // Grass
        new BlockProperties(true, 12)       // Field
    };


    private void Start()
    {
        GenerateWorldMap();
    }


    private void GenerateWorldMap()
    {
        for (int x = 0; x < ChunkNumber; ++x)
            for (int z = 0; z < ChunkNumber; ++z)
                mapChunks[x, z] = new Chunk(this, (x, z));
    }


    private bool IsChunkInWorld((int x, int z) chunkPos)
    {
        if (chunkPos.x < 0 || chunkPos.x > ChunkNumber ||
            chunkPos.z < 0 || chunkPos.z > ChunkNumber)
            return false;

        return true;
    }


    private bool IsBlockInWorld(Vector3 blockPos)
    {
        if (blockPos.x < 0 || blockPos.x > VoxelNumber - 1 ||
            blockPos.y < 0 || blockPos.y > Chunk.Height - 1 ||
            blockPos.z < 0 || blockPos.z > VoxelNumber - 1)
            return false;

        return true;
    }



    public BlockType GetBlockType(Vector3 blockPos)
    {
        if (!IsBlockInWorld(blockPos))
            return BlockType.Air;

        return BlockType.Grass;
    }
}
