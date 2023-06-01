using System.Collections.Generic;
using UnityEngine;


public struct MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    public MeshData(int width, int height)
    {
        vertices = new Vector3[width * height];
        uvs = new Vector2[width * height];
        triangles = new int[(width - 1) * (height - 1) * 6];
    }

    public void AddVertex(int index, Vector3 vertex)
    {
        vertices[index] = vertex;
    }

    public void AddTriangle(int index, int a, int b, int c)
    {
        triangles[index] = a;
        triangles[index + 1] = b;
        triangles[index + 2] = c;
    }

    public void AddUV(int index, Vector2 uv)
    {
        uvs[index] = uv;
    }
}


public class Chunk
{
    public readonly GameObject gameObject;
    private readonly MeshData meshData;
    private readonly Bounds chunkBounds;

    public Vector3 ChunkPosition { get => gameObject.transform.position; }
    public (int x, int z) ChunkIndex { get; private set; }
    public bool IsVisible { get => gameObject.activeSelf; }

    public const int TILE_WIDTH = 50;
    public const int TILE_NUMBER = 5;
    public const int WIDTH = TILE_NUMBER * TILE_WIDTH;

    public const int STEP_HEIGHT = 20;
    public const int MAX_STEPS = 7;
    public const int MAX_HEIGHT = STEP_HEIGHT * MAX_STEPS;

    private readonly (int x, int z)[] vertexOffsets =
    {
        (0, 0),
        (TILE_WIDTH, 0),
        (TILE_WIDTH, 0),
        (TILE_WIDTH, TILE_WIDTH),
        (TILE_WIDTH, TILE_WIDTH),
        (0, TILE_WIDTH),
        (0, TILE_WIDTH),
        (0, 0),
        (TILE_WIDTH / 2, TILE_WIDTH / 2)
    };

    private readonly List<int>[,] vertices = new List<int>[TILE_NUMBER + 1, TILE_NUMBER + 1];
    private readonly int[,] centers = new int[TILE_NUMBER, TILE_NUMBER];

    private readonly House[,] houseAtVertex = new House[TILE_NUMBER + 1, TILE_NUMBER + 1];


    public Chunk((int x, int z) locationInMap)
    {
        gameObject = new GameObject();
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>();
        gameObject.AddComponent<MeshCollider>();

        ChunkIndex = locationInMap;
        gameObject.transform.position = new Vector3(ChunkIndex.x * WIDTH, 0, ChunkIndex.z * WIDTH);
        gameObject.name = "Chunk " + ChunkIndex.x + " " + ChunkIndex.z;
        SetVisibility(false);

        chunkBounds = new(ChunkPosition + new Vector3(WIDTH / 2, 0, WIDTH / 2), new Vector3(WIDTH, 0, WIDTH));

        meshData = GenerateMeshData();
        SetVertexHeights();
    }



    public void SetVisibility(bool isVisible)
    {
        gameObject.SetActive(isVisible);
    }

    public float DistanceFromPoint(Vector3 point) => Mathf.Sqrt(chunkBounds.SqrDistance(point));

    // Inputs are coordinates relative to the chunk
    public (int, int) IndicesFromCoordinates(float x, float z)
        => (Mathf.FloorToInt(x / TILE_WIDTH), Mathf.FloorToInt(z / TILE_WIDTH));

    public (float, float) CoordinatesFromIndices(int x, int z)
        => (x * TILE_WIDTH, z * TILE_WIDTH);

    private float GetVertexHeightAtIndex(int x, int z) => meshData.vertices[vertices[z, x][0]].y;
    private float GetCenterHeightAtIndex(int x, int z) => meshData.vertices[centers[z, x]].y;

    public float GetHeight(float x, float z)
    {
        (int x_i, int z_i) = IndicesFromCoordinates(x, z);

        if (x % TILE_WIDTH == 0 && x % TILE_WIDTH == 0) 
            return GetVertexHeightAtIndex(x_i, z_i);
        else
            return GetCenterHeightAtIndex(x_i, z_i);
    }

    public void SetHouseAtVertex(float x, float z, House house)
    {
        (int x_i, int z_i) = IndicesFromCoordinates(x, z);
        houseAtVertex[z_i, x_i] = house;
    }

    public House GetHouseAtVertex(float x, float z)
    {
        (int x_i, int z_i) = IndicesFromCoordinates(x, z);
        return houseAtVertex[z_i, x_i];
    }



    #region Building Mesh
    private MeshData GenerateMeshData()
    {
        MeshData meshData = new(WIDTH, WIDTH);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int z = 0; z < WIDTH; z += TILE_WIDTH)
        {
            for (int x = 0; x < WIDTH; x += TILE_WIDTH)
            {
                for (int i = 0; i < vertexOffsets.Length; ++i)
                {
                    Vector3 vertex = new(x + vertexOffsets[i].x, 0, z + vertexOffsets[i].z);
                    meshData.AddVertex(vertexIndex + i, vertex);
                    meshData.AddUV(vertexIndex + i, new Vector2(vertex.x / WIDTH, vertex.z / WIDTH));

                    (int x_i, int z_i) = IndicesFromCoordinates(x + vertexOffsets[i].x, z + vertexOffsets[i].z);

                    if (i != vertexOffsets.Length - 1)
                    {
                        if (vertices[z_i, x_i] == null)
                            vertices[z_i, x_i] = new List<int>();

                        vertices[z_i, x_i].Add(vertexIndex + i);
                    }
                    else
                        centers[z_i, x_i] = vertexIndex + i;
                }

                // Add triangles
                for (int i = 0; i < vertexOffsets.Length; i += 2)
                {
                    meshData.AddTriangle(triangleIndex, vertexIndex + i, vertexIndex + vertexOffsets.Length - 1, vertexIndex + i + 1);
                    triangleIndex += 3;
                }

                vertexIndex += vertexOffsets.Length;
            }
        }
        return meshData;
    }

    private void SetVertexHeights()
    {
        for (int z = 0; z < vertices.GetLength(0); ++z)
        {
            for (int x = 0; x < vertices.GetLength(1); ++x)
            {
                List<int> currentVertices = vertices[z, x];
                float height;

                (float x_coord, float z_coord) = CoordinatesFromIndices(x, z);

                if (x == 0 && ChunkIndex.x > 0)
                    height = WorldMap.Instance.GetHeight((ChunkIndex.x - 1, ChunkIndex.z), (WIDTH, z_coord));
                else if (z == 0 && ChunkIndex.z > 0)
                    height = WorldMap.Instance.GetHeight((ChunkIndex.x, ChunkIndex.z - 1), (x_coord, WIDTH));
                else
                    height = CalculateVertexHeight(x, z, currentVertices[0]);

                foreach (int v in currentVertices)
                    meshData.vertices[v].y = height;
            }
        }

        for (int z = 0; z < centers.GetLength(0); ++z)
        {
            for (int x = 0; x < centers.GetLength(1); ++x)
            {
                float height = CalculateCenterHeight(x, z);
                meshData.vertices[centers[z, x]].y = height;
            }
        }
    }

    private float CalculateVertexHeight(int x, int z, int vertexIndex)
    {
        float height = Mathf.FloorToInt(NoiseGenerator.GetPerlinAtPosition(ChunkPosition + meshData.vertices[vertexIndex]) * MAX_STEPS) * STEP_HEIGHT;

        if (x > 0 && z == 0)
        {
            float lastHeight = GetVertexHeightAtIndex(x - 1, z);
            height = Mathf.Clamp(height, lastHeight - STEP_HEIGHT, lastHeight + STEP_HEIGHT);
        }
        else if (z > 0)
        {
            (int, int)[] directions = { (-1, -1), (-1, 0), (0, -1), (1, -1) };

            List<float> neighborHeights = new();

            foreach ((int neighbor_x, int neighbor_z) in directions)
                if (x + neighbor_x >= 0 && x + neighbor_x < vertices.GetLength(1) &&
                    z + neighbor_z >= 0 && z + neighbor_z < vertices.GetLength(0))
                    neighborHeights.Add(GetVertexHeightAtIndex(x + neighbor_x, z + neighbor_z));

            if (neighborHeights.TrueForAll(EqualToFirst))
                height = Mathf.Clamp(height, neighborHeights[0] - STEP_HEIGHT, neighborHeights[0] + STEP_HEIGHT);
            else
            {
                float[] neighborHeightsArray = neighborHeights.ToArray();
                float minHeight = Mathf.Min(neighborHeightsArray);
                float maxHeight = Mathf.Max(neighborHeightsArray);

                if (Mathf.Abs(minHeight - maxHeight) > STEP_HEIGHT)
                    height = minHeight + STEP_HEIGHT;
                else
                    height = Mathf.Clamp(height, minHeight, maxHeight);
            }

            bool EqualToFirst(float x) => x == neighborHeights[0];
        }

        return height;
    }

    private float CalculateCenterHeight(int x, int z)
    {
        float[] cornerHeights = new float[4];
        int i = 0;
        for (int zOffset = 0; zOffset <= 1; ++zOffset)
            for (int xOffset = 0; xOffset <= 1; ++xOffset)
                cornerHeights[i++] = GetVertexHeightAtIndex(x + xOffset, z + zOffset);

        if (cornerHeights[0] == cornerHeights[3] && cornerHeights[1] == cornerHeights[2])
            return Mathf.Max(cornerHeights);
        else
            return Mathf.Min(cornerHeights) + (STEP_HEIGHT / 2);
    }

    public void SetMesh()
    {
        Mesh mesh = new()
        {
            name = "Chunk Mesh",
            vertices = meshData.vertices,
            triangles = meshData.triangles,
            uv = meshData.uvs
        };

        mesh.RecalculateNormals();

        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }
    #endregion



    #region Modifying Mesh
    public List<(int x, int z, float height, bool isCenter)> UpdateHeights((float x, float z) coords, bool decrease)
    {
        List<(int x, int z, float height, bool isCenter)> modifiedVertices = new();

        (int x, int z) = IndicesFromCoordinates(coords.x, coords.z);
        UpdateHeightAtPoint(x, z, decrease, ref modifiedVertices);

        return modifiedVertices;
    }


    private void UpdateHeightAtPoint(int x, int z, bool decrease, ref List<(int x, int z, float height, bool isCenter)> modifiedVertices)
    {
        // Get all the vertices that share the clicked point
        List<int> currentVertices = vertices[z, x];

        // Update the heights of all the vertices at the current point
        foreach (int v in currentVertices)
        {
            if (decrease && meshData.vertices[v].y > 0)
                meshData.vertices[v].y -= STEP_HEIGHT;

            if (!decrease && meshData.vertices[v].y < MAX_HEIGHT)
                meshData.vertices[v].y += STEP_HEIGHT;
        }

        if (houseAtVertex[z, x] != null)
            houseAtVertex[z, x].OnDestroyHouse(true);

        modifiedVertices.Add((x, z, GetVertexHeightAtIndex(x, z), false));

        //Update neighboring vertices in current chunk
        for (int zOffset = -1; zOffset <= 1; ++zOffset)
        {
            for (int xOffset = -1; xOffset <= 1; ++xOffset)
            {
                int neighborX = x + xOffset;
                int neighborZ = z + zOffset;

                // check if we're out of bounds of the current chunk
                if ((xOffset, zOffset) != (0, 0))
                {
                    if (neighborZ >= 0 && neighborZ < vertices.GetLength(0) && neighborX >= 0 && neighborX < vertices.GetLength(1))
                    {
                        if (Mathf.Abs(meshData.vertices[currentVertices[0]].y - GetVertexHeightAtIndex(neighborX, neighborZ)) > STEP_HEIGHT)
                            UpdateHeightAtPoint(neighborX, neighborZ, decrease, ref modifiedVertices);
                    }
                    else
                    {
                        (float x, float z) coords = CoordinatesFromIndices(x, z);

                        WorldMap.Instance.UpdateVertexInChunk(
                            (neighborX < 0 ? ChunkIndex.x - 1 : (neighborX >= vertices.GetLength(1) ? ChunkIndex.x + 1 : ChunkIndex.x),
                             neighborZ < 0 ? ChunkIndex.z - 1 : (neighborZ >= vertices.GetLength(0) ? ChunkIndex.z + 1 : ChunkIndex.z)),
                            (neighborX < 0 ? WIDTH : (neighborX >= vertices.GetLength(1) ? 0 : coords.x),
                             neighborZ < 0 ? WIDTH : (neighborZ >= vertices.GetLength(0) ? 0 : coords.z)),
                            GetVertexHeightAtIndex(x, z),
                            decrease);
                    }
                }
            }
        }

        // Recompute centers
        for (int center_z = -1; center_z <= 0; ++center_z)
        {
            for (int center_x = -1; center_x <= 0; ++center_x)
            {
                (int x, int z) center = (x + center_x, z + center_z);

                if (center.x >= 0 && center.z >= 0 && center.x < centers.GetLength(1) && center.z < centers.GetLength(0))
                {
                    float height = CalculateCenterHeight(center.x, center.z);
                    meshData.vertices[centers[center.z, center.x]].y = height;
                    modifiedVertices.Add((center.x, center.z, height, true));
                }
            }
        }       
    }


    public void SetVertexHeightAtPoint(int x, int z, float height, bool isCenter)
    {
        if (!isCenter)
            foreach (var v in vertices[x, z])
                meshData.vertices[v].y = height;

        if (isCenter)
            meshData.vertices[centers[x, z]].y = height;
    }
    #endregion
}
