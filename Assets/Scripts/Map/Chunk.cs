using UnityEngine;


public class MeshData
{
    public Vector3[] Vertices { get; }
    public int[] Triangles { get; }
    public Vector2[] Uvs { get; }

    public MeshData(int width, int height)
    {
        Vertices = new Vector3[width * width * height * (BlockData.Faces * BlockData.VerticesPerFace)];
        Uvs = new Vector2[Vertices.Length];
        Triangles = new int[width * width * height * (BlockData.Faces * 6)];
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

    public const int Width = 1;
    public const int Height = 1;

    private readonly Block[,,] blockMap = new Block[Width, Height, Width];


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

        return blockMap[x, y, z].Type;
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
                    blockMap[x, y, z] = new Block(WorldMap.Instance.GenerateBlock(new Vector3(x, y, z) + Coordinates));
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
                    if (WorldMap.BlockTypes[(int)blockMap[x, y, z].Type].IsSolid)
                        AddBlockToMesh(chunkData, new Vector3(x, y, z), ref vertexIndex, ref triangleIndex);

        return chunkData;
    }

    private void AddBlockToMesh(MeshData chunkData, Vector3 blockPosition, ref int vertexIndex, ref int triangleIndex)
    {
        // get coordinates for texture from texture atlas
        Block block = blockMap[(int)blockPosition.x, (int)blockPosition.y, (int)blockPosition.z];
        Vector2 topTextureCoords = GetTextureCoordinates(WorldMap.BlockTypes[(int)block.Type].TopTextureId);

        for (int face = 0; face < BlockData.Faces; ++face)
        {
            // if the face we're drawing has a neighboring face, it shouldn't be drawn
            if (!IsBlockSolid(blockPosition + BlockData.NeighborBlockFace[face]))
            {
                for (int i = 0; i < BlockData.VerticesPerFace; ++i)
                {
                    int index = BlockData.FaceVertices[face, i];
                    if (BlockData.IsTopVertex[index])
                        chunkData.AddVertex(vertexIndex + i, blockPosition + block.GetVertex(index));
                    else
                        chunkData.AddVertex(vertexIndex + i, blockPosition + BlockData.VertexOffsets[index]);
                        
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

        return WorldMap.BlockTypes[(int)blockMap[(int)blockPosition.x, (int)blockPosition.y, (int)blockPosition.z].Type].IsSolid;
    }



    public void ModifyBlock(Vector3 hitPosition, bool remove)
    {
        (int x, int y, int z) = GetBlockIndexFromCoordinates(new Vector3(hitPosition.x, hitPosition.y, hitPosition.z));

        int vertex;
        float slack = 0.2f;

        Vector3 distance = hitPosition - new Vector3(x, y, z);

        if (distance.x >= BlockData.VertexOffsets[2].x - slack &&
            distance.y >= BlockData.VertexOffsets[2].y - slack &&
            distance.z <= BlockData.VertexOffsets[2].z + slack)
            vertex = 2;
        else if (distance.x <= BlockData.VertexOffsets[3].x + slack &&
            distance.y >= BlockData.VertexOffsets[3].y - slack &&
            distance.z <= BlockData.VertexOffsets[3].z + slack)
            vertex = 3;
        else if (distance.x >= BlockData.VertexOffsets[6].x - slack &&
            distance.y >= BlockData.VertexOffsets[6].y - slack &&
            distance.z >= BlockData.VertexOffsets[6].z - slack)
            vertex = 6;
        else if (distance.x <= BlockData.VertexOffsets[7].x + slack &&
            distance.y >= BlockData.VertexOffsets[7].y - slack &&
            distance.z >= BlockData.VertexOffsets[7].z - slack)
            vertex = 7;
        else
            return;

        Debug.Log($"{x}|{y}|{z} vertex " + vertex);

        bool update = false;
        if (remove && blockMap[x, y + 1, z].Type == BlockType.Air)
        {
            GoDown(x, y, z, vertex);
            update = true;
        }           

        //if (remove && blockMap[x, y + 1, z] == BlockType.Air && y != 0)  // we can only remove the top block and we cannot remove the bottom row
        //{
        //    blockMap[x, y, z] = BlockType.Air;
        //    update = true;
        //}

        //if (!remove && blockMap[x, y, z] != BlockType.Air && blockMap[x, y + 1, z] == BlockType.Air && y + 1 < Height)
        //{
        //    blockMap[x, y + 1, z] = BlockType.Grass;
        //    update = true;
        //}

        if (update)
        {
            UpdateMesh();
            UpdateSurroundingChunks(hitPosition);
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



    private void GoDown(int x, int y, int z, int vertex)
    {
        Block block = blockMap[x, y, z];
        float vertexHeight = block.GetVertex(vertex).y;
        float diagonalVertexHeight = block.GetDiagonalVertex(vertex).y;

        if (vertexHeight == BlockData.Height && diagonalVertexHeight == BlockData.Height)
            block.LowerVertex(vertex);
        else if (vertexHeight == BlockData.Height && diagonalVertexHeight == 0)
            block.Type = BlockType.Air;
        else if (vertexHeight == 0 && diagonalVertexHeight == BlockData.Height)
        {
            block.Type = BlockType.Air;

            if (y - 1 > 1)
                blockMap[x, y - 1, z].LowerVertex(vertex);
        }

        // all 8 adjacent side blocks need to be updated
    }

    private void IncreaseVertex(int x, int y, int z, int vertex)
    {
        // case 1:
        // current = 1, diagonal = 1
        // add block above with current = 1, others = 0

        // case 2
        // current = 1, diagonal = 0
        // diagonal = 1, goto case 1

        // case 3
        // current = 0, diagonal = 1
        // restore to normal cube

        // all 8 adjacent side blocks need to be updated

    }
}