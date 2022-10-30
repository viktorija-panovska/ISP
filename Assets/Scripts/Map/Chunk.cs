using UnityEngine;


# region  Block

public enum BlockType
{
    None,
    Air,
    Water,
    Grass
}


public struct BlockProperties
{
    public bool IsSolid { get; }
    public int TopTextureId { get; }
    public int SideTextureId { get; }

    public BlockProperties(bool isSolid, int topTextureId, int sideTextureId)
    {
        IsSolid = isSolid;
        TopTextureId = topTextureId;
        SideTextureId = sideTextureId;
    }
}


public struct BlockData
{
    public const int Width = 1;
    public const int Height = 1;
    public const int Vertices = 8;
    public const int Faces = 6;
    public const int VerticesPerFace = 4;

    public static readonly Vector3[] VertexOffsets = new Vector3[Vertices]
    {
        new Vector3(0,     0,      0),       // front lower left
        new Vector3(Width, 0,      0),       // front lower right
        new Vector3(Width, Height, 0),       // front upper right
        new Vector3(0,     Height, 0),       // front upper left
        new Vector3(0,     0,      Width),   // back lower left
        new Vector3(Width, 0,      Width),   // back lower right
        new Vector3(Width, Height, Width),   // back upper right
        new Vector3(0,     Height, Width),   // back upper left
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
        new Vector2(WorldMap.Instance.NormalizedTextureBlockSize, 0),
        new Vector2(WorldMap.Instance.NormalizedTextureBlockSize, WorldMap.Instance.NormalizedTextureBlockSize),
        new Vector2(0, WorldMap.Instance.NormalizedTextureBlockSize)
    };

    // This is the vector that points us to the face of the neighboring voxel that touches
    // the chosen face of this block
    public static readonly Vector3[] NeighborBlockFace = new Vector3[Faces]
    {
        new Vector3(0, 0, -1),    // front face
        new Vector3(0, 0, 1),     // back face
        new Vector3(0, 1, 0),     // top face
        new Vector3(0, -1, 0),    // bottom face
        new Vector3(-1, 0, 0),    // left face
        new Vector3(1, 0, 0)      // right face
    };

    public static readonly bool[] IsSideFace = new bool[] 
    { 
        true,
        true,
        false,
        false,
        true,
        true
    };
}

# endregion



public class MeshData
{
    public Vector3[] Vertices { get; }
    public int[] Triangles { get; }
    public Vector2[] Uvs { get; }

    public MeshData(int width, int height)
    {
        Vertices = new Vector3[width * height * (BlockData.Faces * BlockData.VerticesPerFace)];
        Uvs = new Vector2[Vertices.Length];
        Triangles = new int[width * height * (BlockData.Faces * 6)];
    }

    public void AddVertex(int index, Vector3 vertex)
    {
        Vertices[index] = vertex;
    }

    public void AddTriangles(int index, int a1, int b1, int c1, int a2, int b2, int c2)
    {
        Triangles[index] = a1;
        Triangles[index + 1] = b1;
        Triangles[index + 2] = c1;

        Triangles[index + 3] = a2;
        Triangles[index + 4] = b2;
        Triangles[index + 5] = c2;
    }

    public void AddUV(int index, Vector2 uv)
    {
        Uvs[index] = uv;
    }
}


public class Chunk
{
    private readonly GameObject gameObject;

    public Vector3 Coordinates { get => gameObject.transform.position; }
    public (int x, int z) Index { get; private set; }

    public const int Width = 5;
    public const int Height = 15;

    private readonly BlockType[,,] blockMap = new BlockType[Width, Height, Width];



    public Chunk(WorldMap worldMap, (int x, int z) locationInMap)
    {
        gameObject = new GameObject();
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>();
        gameObject.AddComponent<MeshCollider>();
        gameObject.GetComponent<MeshRenderer>().material = worldMap.WorldMaterial;
        gameObject.transform.SetParent(WorldMap.Instance.Transform);

        Index = locationInMap;
        gameObject.transform.position = new Vector3(Index.x * Width, 0f, Index.z * Width);
        gameObject.name = "Chunk " + Index.x + " " + Index.z;

        GenerateBlockMap();
        UpdateMesh();
    }



    public BlockType GetBlockAtIndex(int x, int y, int z)
    {
        if (x < 0 || x >= Width ||
            y < 0 || y >= Height ||
            z < 0 || z >= Width)
            return BlockType.None;

        return blockMap[x, y, z];
    }

    public (int x, int y, int z) GetBlockIndexFromCoordinates(Vector3 position) => (
        Mathf.FloorToInt(position.x) - Mathf.FloorToInt(Coordinates.x),
        Mathf.FloorToInt(position.y),
        Mathf.FloorToInt(position.z) - Mathf.FloorToInt(Coordinates.z));

    private Vector2 GetTextureCoordinates(int textureId)
    {
        float y = (textureId / WorldMap.TextureAtlasBlocks);
        float x = (textureId - (y * WorldMap.TextureAtlasBlocks)) * WorldMap.Instance.NormalizedTextureBlockSize;

        y *= WorldMap.Instance.NormalizedTextureBlockSize;

        return new Vector2(x, y);
    }



    private void GenerateBlockMap()
    {
        for (int y = 0; y < Height; ++y)
            for (int x = 0; x < Width; ++x)
                for (int z = 0; z < Width; ++z)
                    blockMap[x, y, z] = WorldMap.Instance.GenerateBlock(new Vector3(x, y, z) + Coordinates);
    }

    private void UpdateMesh()
    {
        DrawMesh(GenerateMeshData());
    }

    private MeshData GenerateMeshData()
    {
        MeshData chunkData = new MeshData(Width, Height);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int y = 0; y < Height; ++y)
            for (int x = 0; x < Width; ++x)
                for (int z = 0; z < Width; ++z)
                    if (WorldMap.BlockTypes[(int)blockMap[x, y, z]].IsSolid)
                        AddBlockToMesh(chunkData, new Vector3(x, y, z), ref vertexIndex, ref triangleIndex);

        return chunkData;
    }

    private void AddBlockToMesh(MeshData chunkData, Vector3 blockPosition, ref int vertexIndex, ref int triangleIndex)
    {
        // get coordinates for texture from texture atlas
        BlockType blockType = blockMap[(int)blockPosition.x, (int)blockPosition.y, (int)blockPosition.z];
        Vector2 topTextureCoords = GetTextureCoordinates(WorldMap.BlockTypes[(int)blockType].TopTextureId);
        Vector2 sideTextureCoords = GetTextureCoordinates(WorldMap.BlockTypes[(int)blockType].SideTextureId);

        for (int face = 0; face < BlockData.Faces; ++face)
        {
            // if the face we're drawing has a neighboring face, it shouldn't be drawn
            if (!IsBlockSolid(blockPosition + BlockData.NeighborBlockFace[face]))
            {
                for (int i = 0; i < BlockData.VerticesPerFace; ++i)
                {
                    chunkData.AddVertex(vertexIndex + i, blockPosition + BlockData.VertexOffsets[BlockData.FaceVertices[face, i]]);

                    if (BlockData.IsSideFace[face])
                        chunkData.AddUV(vertexIndex + i, sideTextureCoords + BlockData.UvOffsets[i]);
                    else
                        chunkData.AddUV(vertexIndex + i, topTextureCoords + BlockData.UvOffsets[i]);
                }

                chunkData.AddTriangles(triangleIndex, vertexIndex, vertexIndex + 2, vertexIndex + 1, vertexIndex + 2, vertexIndex, vertexIndex + 3);

                vertexIndex += 4;
                triangleIndex += 6;
            }
        }
    }

    private void DrawMesh(MeshData meshData)
    {
        Mesh mesh = new Mesh()
        {
            name = "Chunk Mesh",
            vertices = meshData.Vertices,
            triangles = meshData.Triangles,
            uv = meshData.Uvs
        };

        mesh.RecalculateNormals();

        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }



    private bool IsBlockInChunk(Vector3 blockPosition)
    {
        if (blockPosition.x < 0 || blockPosition.x >= Width ||
            blockPosition.y < 0 || blockPosition.y >= Height ||
            blockPosition.z < 0 || blockPosition.z >= Width)
            return false;

        return true;
    }

    private bool IsBlockSolid(Vector3 blockPosition)
    {
        if (!IsBlockInChunk(blockPosition))
            return WorldMap.BlockTypes[(int)WorldMap.Instance.GetBlockTypeAtPosition(blockPosition + Coordinates)].IsSolid;

        return WorldMap.BlockTypes[(int)blockMap[(int)blockPosition.x, (int)blockPosition.y, (int)blockPosition.z]].IsSolid;
    }



    public void ModifyBlock(Vector3 blockPosition, bool remove)
    {
        (int x, int y, int z) = GetBlockIndexFromCoordinates(new Vector3(blockPosition.x, blockPosition.y - 0.5f, blockPosition.z));

        bool update = false;
        if (remove && blockMap[x, y + 1, z] == BlockType.Air && y != 0)  // we can only remove the top block and we cannot remove the bottom row
        {
            blockMap[x, y, z] = BlockType.Air;
            update = true;
        }

        if (!remove && blockMap[x, y, z] != BlockType.Air && blockMap[x, y + 1, z] == BlockType.Air && y + 1 < Height)
        {
            blockMap[x, y + 1, z] = BlockType.Grass;
            update = true;
        }

        if (update)
        {
            UpdateMesh();
            UpdateSurroundingChunks(blockPosition);
        }
    }

    private void UpdateSurroundingChunks(Vector3 blockPosition)
    {
        for (int face = 0; face < BlockData.Faces; ++face)
        {
            if (BlockData.IsSideFace[face])
            {
                Vector3 neighborPos = blockPosition + BlockData.NeighborBlockFace[face];

                if (!IsBlockInChunk(neighborPos) && WorldMap.Instance.IsChunkInWorld(neighborPos))
                    WorldMap.Instance.GetChunkAtCoordinates(neighborPos).UpdateMesh();                 
            }
        }
    }
}