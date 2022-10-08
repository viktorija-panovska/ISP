using UnityEngine;



public enum BlockType
{
    Air,
    Water,
    Grass,
    Field
}


public struct BlockProperties
{
    public bool IsSolid;
    public int TextureId;

    public BlockProperties(bool isSolid, int textureId)
    {
        IsSolid = isSolid;
        TextureId = textureId;
    }
}


public struct Block
{
    public const int Vertices = 8;
    public const int Faces = 6;
    public const int VerticesPerFace = 4;

    public static readonly Vector3[] VertexOffsets = new Vector3[Vertices]
    {
        new Vector3(0, 0, 0),   // front lower left
        new Vector3(1, 0, 0),   // front lower right
        new Vector3(1, 1, 0),   // front upper right
        new Vector3(0, 1, 0),   // front upper left
        new Vector3(0, 0, 1),   // back lower left
        new Vector3(1, 0, 1),   // back lower right
        new Vector3(1, 1, 1),   // back upper right
        new Vector3(0, 1, 1),   // back upper left
    };

    // These are the indeces of the vertices on each face in the VertexOffsets array
    public static readonly int[,] FaceVertices = new int[Faces, VerticesPerFace]
    {
        { 0, 1, 2, 3 },   // front face
        { 5, 4, 7, 6 },   // back face
        { 3, 2, 6, 7 },   // top face
        { 0, 1, 5, 4 },   // bottom face
        { 4, 0, 3, 7 },   // left face
        { 1, 5, 6, 2 },   // right face
    };

    public static readonly Vector2[] UvOffsets = new Vector2[VerticesPerFace]
    {
        new Vector2(0, 0),
        new Vector2(0, WorldMap.NormalizedTextureBlockSize),
        new Vector2(WorldMap.NormalizedTextureBlockSize, 0),
        new Vector2(WorldMap.NormalizedTextureBlockSize, WorldMap.NormalizedTextureBlockSize)
    };

    // This is the vector that points us to the face of the neighboring voxel that touches
    // the chosen face of this voxel
    public static readonly Vector3[] NeighborVoxelFace = new Vector3[Faces]
    {
        new Vector3(0, 0, -1),    // front face
        new Vector3(0, 0, 1),     // back face
        new Vector3(0, 1, 0),     // top face
        new Vector3(0, -1, 0),    // bottom face
        new Vector3(-1, 0, 0),    // left face
        new Vector3(1, 0, 0)      // right face
    };
}




public class Chunk
{
    private WorldMap worldMap;
    private GameObject chunkObject;

    public Vector3 PositionInGameWorld { get => chunkObject.transform.position; }
    public (int x, int z) PositionInWorldMap;

    public const int Width = 5;
    public const int Height = 15;

    private BlockType[,,] blockTypes = new BlockType[Width, Height, Width];



    public Chunk(WorldMap worldMap, (int, int) chunkLoc)
    {
        this.worldMap = worldMap;
        chunkObject = new GameObject();
        chunkObject.AddComponent<MeshFilter>();
        chunkObject.AddComponent<MeshRenderer>();
        chunkObject.AddComponent<MeshCollider>();
        chunkObject.GetComponent<MeshRenderer>().material = worldMap.WorldMaterial;
        chunkObject.transform.SetParent(worldMap.transform);

        PositionInWorldMap = chunkLoc;
        chunkObject.transform.position = new Vector3(PositionInWorldMap.x * Width, 0f, PositionInWorldMap.z * Width);
        chunkObject.name = "Chunk " + PositionInWorldMap.x + " " + PositionInWorldMap.z;

        FillChunkMap();
        DrawMesh(GenerateChunkData());
    }


    private void FillChunkMap()
    {
        for (int y = 0; y < Height; ++y)
            for (int x = 0; x < Width; ++x)
                for (int z = 0; z < Width; ++z)
                    blockTypes[x, y, z] = worldMap.GetBlockType(new Vector3(x, y, z) + PositionInGameWorld);
    }


    private ChunkData GenerateChunkData()
    {
        ChunkData chunkData = new ChunkData(Width, Height);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int y = 0; y < Height; ++y)
            for (int x = 0; x < Width; ++x)
                for (int z = 0; z < Width; ++z)
                    AddBlockToChunk(chunkData, new Vector3(x, y, z), ref vertexIndex, ref triangleIndex);

        return chunkData;
    }


    private void AddBlockToChunk(ChunkData chunkData, Vector3 blockPosition, ref int vertexIndex, ref int triangleIndex)
    {
        // get coordinates for texture from texture atlas
        BlockType blockType = blockTypes[(int)blockPosition.x, (int)blockPosition.y, (int)blockPosition.z];
        int textureID = WorldMap.BlockTypes[(int)blockType].TextureId;
        float y = textureID / WorldMap.TextureAtlasBlocks * WorldMap.NormalizedTextureBlockSize;
        float x = (textureID - (y * WorldMap.TextureAtlasBlocks)) * WorldMap.NormalizedTextureBlockSize;
        Vector2 textureCoords = new Vector2(x, y);

        for (int face = 0; face < Block.Faces; ++face)
        {
            // if the face we're drawing has a neighboring face, it shouldn't be drawn
            if (!IsBlockSolid(blockPosition + Block.NeighborVoxelFace[face]))
            {
                for (int i = 0; i < Block.VerticesPerFace; ++i)
                {
                    chunkData.AddVertex(vertexIndex + i, blockPosition + Block.VertexOffsets[Block.FaceVertices[face, i]]);
                    chunkData.AddUV(vertexIndex + i, textureCoords + Block.UvOffsets[i]);
                }

                chunkData.AddTriangles(triangleIndex, vertexIndex, vertexIndex + 2, vertexIndex + 1, vertexIndex + 2, vertexIndex, vertexIndex + 3);

                vertexIndex += 4;
                triangleIndex += 6;
            }
        }
    }


    private bool IsBlockInChunk(Vector3 blockPos)
    {
        if (blockPos.x < 0 || blockPos.x > Width - 1 ||
            blockPos.y < 0 || blockPos.y > Height - 1 ||
            blockPos.z < 0 || blockPos.z > Width - 1)
            return false;

        return true;
    }


    private bool IsBlockSolid(Vector3 pos)
    {
        if (!IsBlockInChunk(pos))
            return WorldMap.BlockTypes[(int)worldMap.GetBlockType(pos + PositionInGameWorld)].IsSolid;

        return WorldMap.BlockTypes[(int)blockTypes[(int)pos.x, (int)pos.y, (int)pos.z]].IsSolid;
    }


    private void DrawMesh(ChunkData meshData)
    {
        Mesh mesh = new Mesh()
        {
            name = "Chunk Mesh",
            vertices = meshData.vertices,
            triangles = meshData.triangles,
            uv = meshData.uvs
        };

        mesh.RecalculateNormals();

        chunkObject.GetComponent<MeshFilter>().mesh = mesh;
        chunkObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }
}