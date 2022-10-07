using UnityEngine;

public class WorldMap : MonoBehaviour
{
    public const int WorldChunks = 5;
    public static int WorldVoxels { get => WorldChunks * Chunk.Width; }
    private Chunk[,] mapChunks = new Chunk[WorldChunks, WorldChunks];

    public VoxelType[] VoxelTypes;

    // Texture
    public Material WorldMaterial;
    public const int TextureAtlasBlocks = 4;
    public static float NormalizedTextureBlockSize { get => 1f / (float)TextureAtlasBlocks; }



    private void Start()
    {
        GenerateWorldMap();
    }


    private void GenerateWorldMap()
    {
        for (int x = 0; x < WorldChunks; ++x)
            for (int z = 0; z < WorldChunks; ++z)
                CreateChunk(x, z);
    }


    private void CreateChunk(int x, int z)
    {
        mapChunks[x, z] = new Chunk(this, (x, z));
    }


    private bool IsChunkInWorld(int x, int z)
    {

        if (x < 0 || x > WorldChunks ||
            z < 0 || z > WorldChunks)
            return false;

        return true;
    }


    private bool IsVoxelInWorld(Vector3 pos)
    {
        if (pos.x < 0 || pos.x > WorldVoxels - 1 ||
            pos.y < 0 || pos.y > Chunk.Height - 1 ||
            pos.z < 0 || pos.z > WorldVoxels - 1)
            return false;

        return true;
    }


    public byte GetVoxel(Vector3 pos)
    {
        if (!IsVoxelInWorld(pos))
            return 0;

        return 2;
    }
}
