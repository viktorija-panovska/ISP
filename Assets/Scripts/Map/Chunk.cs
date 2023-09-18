using System.Collections.Generic;
using UnityEngine;


public class Chunk
{
    private struct MeshData
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
        (TILE_WIDTH / 2, TILE_WIDTH / 2),
        (TILE_WIDTH / 2, TILE_WIDTH / 2),
        (TILE_WIDTH / 2, TILE_WIDTH / 2),
        (TILE_WIDTH / 2, TILE_WIDTH / 2)
    };

    private readonly List<int>[,] vertices = new List<int>[TILE_NUMBER + 1, TILE_NUMBER + 1];
    private readonly List<int>[,] centers = new List<int>[TILE_NUMBER, TILE_NUMBER];

    private readonly IHouse[,] houseAtVertex = new IHouse[TILE_NUMBER + 1, TILE_NUMBER + 1];
    private readonly Dictionary<(int x, int z), NaturalFormation> formations = new();



    #region Chunk Properties

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

        foreach (((int, int) _, var formation) in formations)
            formation.gameObject.SetActive(isVisible);
    }

    public float DistanceFromPoint(Vector3 point) => Mathf.Sqrt(chunkBounds.SqrDistance(point));

    #endregion



    #region Coordinate Translation

    private (int, int) CoordsToIndices(int x, int z)
        => (Mathf.FloorToInt(x / TILE_WIDTH), Mathf.FloorToInt(z / TILE_WIDTH));

    private (int x_coords, int z_coords) VertexIndicesToCoords(int x, int z)
        => (x * TILE_WIDTH, z * TILE_WIDTH);

    private (int, int) CenterIndicesToCoords(int x, int z)
        => ((x + (x + 1)) * (TILE_WIDTH / 2), (z + (z + 1)) * (TILE_WIDTH / 2));

    #endregion



    #region Mesh Height

    private int GetVertexHeightAtIndex(int x, int z) => (int)meshData.vertices[vertices[z, x][0]].y;
    private int GetCenterHeightAtIndex(int x, int z) => (int)meshData.vertices[centers[z, x][0]].y;

    public int GetHeight(int x, int z)
    {
        (int x_i, int z_i) = CoordsToIndices(x, z);

        if (x % TILE_WIDTH == 0 && x % TILE_WIDTH == 0) 
            return GetVertexHeightAtIndex(x_i, z_i);
        else
            return GetCenterHeightAtIndex(x_i, z_i);
    }

    #endregion



    #region Houses

    public void SetHouseAtVertex(int x, int z, IHouse house)
    {
        (int x_i, int z_i) = CoordsToIndices(x, z);
        houseAtVertex[z_i, x_i] = house;
    }

    public IHouse GetHouseAtVertex(int x, int z)
    {
        (int x_i, int z_i) = CoordsToIndices(x, z);
        return houseAtVertex[z_i, x_i];
    }

    #endregion



    #region Space Accessibility


    public bool IsSpaceTree(int x, int z)
    => formations.ContainsKey(CoordsToIndices(x, z)) && formations[CoordsToIndices(x, z)].Type == FormationTypes.Tree;

    public bool IsSpaceRock(int x, int z)
        => formations.ContainsKey(CoordsToIndices(x, z)) && formations[CoordsToIndices(x, z)].Type == FormationTypes.Rock;

    public bool IsSpaceSwamp(int x, int z)
        => formations.ContainsKey(CoordsToIndices(x, z)) && formations[CoordsToIndices(x, z)].Type == FormationTypes.Swamp;

    public bool IsSpaceForest(int x, int z)
        => formations.ContainsKey(CoordsToIndices(x, z)) &&
           (formations[CoordsToIndices(x, z)].Type == FormationTypes.Tree || formations[CoordsToIndices(x, z)].Type == FormationTypes.Rock);

    public bool IsSpaceUnderwater(int x, int z)
    {
        (int x, int z) index = CoordsToIndices(x, z);
        return GetVertexHeightAtIndex(index.x, index.z) <= GameController.Instance.WaterLevel;
    }

    #endregion



    #region Natural Formation

    public void SetFormationAtVertex(int x, int z, NaturalFormation formation)
    {
        (int, int) index = CoordsToIndices(x, z);
        formations.Add(index, formation);
    }

    public void DestroyUnderwaterFormations()
    {
        List<(int x, int z)> underwater = new();

        foreach ((int x, int z) key in formations.Keys)
            if (GetVertexHeightAtIndex(key.x, key.z) <= GameController.Instance.WaterLevel)
                underwater.Add(key);

        foreach ((int x, int z) in underwater)
        {
            GameController.Instance.DestroyFormation(formations[(x, z)].gameObject);
            formations.Remove((x, z));
        }
    }

    #endregion



    #region Mesh Building

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

                    (int x_i, int z_i) = CoordsToIndices(x + vertexOffsets[i].x, z + vertexOffsets[i].z);

                    if (i < 8)
                    {
                        if (vertices[z_i, x_i] == null)
                            vertices[z_i, x_i] = new List<int>();

                        vertices[z_i, x_i].Add(vertexIndex + i);
                    }
                    else
                    {
                        if (centers[z_i, x_i] == null)
                            centers[z_i, x_i] = new List<int>();

                        centers[z_i, x_i].Add(vertexIndex + i);
                    }
                }

                // Add triangles
                for (int i = 0; i < vertexOffsets.Length - 4; i += 2)
                {
                    meshData.AddTriangle(triangleIndex, vertexIndex + i, vertexIndex + 8 + (i / 2), vertexIndex + i + 1);
                    triangleIndex += 3;
                }

                vertexIndex += vertexOffsets.Length;
            }
        }

        return meshData;
    }

    private void SetVertexHeights()
    {
        if (ChunkIndex.x > 0)
        {
            for (int z = 0; z < vertices.GetLength(0); ++z)
            {
                int height = WorldMap.Instance.GetHeight((ChunkIndex.x - 1, ChunkIndex.z), (WIDTH, VertexIndicesToCoords(0, z).z_coords));

                foreach (int v in vertices[z, 0])
                    meshData.vertices[v].y = height;
            }
        }

        if (ChunkIndex.z > 0)
        {
            for (int x = 0; x < vertices.GetLength(1); ++x)
            {
                int height = WorldMap.Instance.GetHeight((ChunkIndex.x, ChunkIndex.z - 1), (VertexIndicesToCoords(x, 0).x_coords, WIDTH));

                foreach (int v in vertices[0, x])
                    meshData.vertices[v].y = height;
            }
        }


        for (int z = 0; z < vertices.GetLength(0); ++z)
        {
            for (int x = 0; x < vertices.GetLength(1); ++x)
            {
                if ((x == 0 && ChunkIndex.x > 0) || (z == 0 && ChunkIndex.z > 0)) continue;

                int height = CalculateVertexHeight(x, z, vertices[z, x][0]);

                foreach (int v in vertices[z, x])
                    meshData.vertices[v].y = height;
            }
        }


        for (int z = 0; z < centers.GetLength(0); ++z)
        {
            for (int x = 0; x < centers.GetLength(1); ++x)
            {
                int height = CalculateCenterHeight(x, z);

                foreach (int c in centers[z, x])
                    meshData.vertices[c].y = height;
            }
        }
    }

    private int CalculateVertexHeight(int x, int z, int vertexIndex)
    {
        int height = Mathf.FloorToInt(NoiseGenerator.GetPerlinAtPosition(ChunkPosition + meshData.vertices[vertexIndex]) * MAX_STEPS) * STEP_HEIGHT;


        // Vertex (0, 0) in chunk (0, 0)
        if (x == 0 && z == 0)
            return height;
        
        // first row in chunk (0, 0)
        if (z == 0 && ChunkIndex.x == 0)
        {
            int lastHeight = GetVertexHeightAtIndex(x - 1, z);
            height = Mathf.Clamp(height, lastHeight - STEP_HEIGHT, lastHeight + STEP_HEIGHT);
        }
        else
        {
            List<(int, int)> directions = new (new (int, int)[]{ (-1, -1), (-1, 0), (0, -1), (1, -1) });

            if (x == 1 && ChunkIndex.x > 0)
                directions.Add((-1, 1));

            List<int> neighborHeights = new();

            foreach ((int neighbor_x, int neighbor_z) in directions)
                if (x + neighbor_x >= 0 && x + neighbor_x < vertices.GetLength(1) && z + neighbor_z >= 0 && z + neighbor_z < vertices.GetLength(0))
                    neighborHeights.Add(GetVertexHeightAtIndex(x + neighbor_x, z + neighbor_z));

            if (neighborHeights.TrueForAll(EqualToFirst))
                height = Mathf.Clamp(height, Mathf.Max(0, neighborHeights[0] - STEP_HEIGHT), Mathf.Min(neighborHeights[0] + STEP_HEIGHT, MAX_HEIGHT));
            else
            {
                int[] neighborHeightsArray = neighborHeights.ToArray();
                int minHeight = Mathf.Min(neighborHeightsArray);
                int maxHeight = Mathf.Max(neighborHeightsArray);

                if (Mathf.Abs(minHeight - maxHeight) > STEP_HEIGHT)
                    height = minHeight + STEP_HEIGHT;
                else
                    height = Mathf.Clamp(height, minHeight, maxHeight);
            }
            bool EqualToFirst(int x) => x == neighborHeights[0];
        }

        return height;
    }

    private int CalculateCenterHeight(int x, int z)
    {
        int[] cornerHeights = new int[4];
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



    #region Mesh Modifying

    public void UpdateHeights((int x, int z) coords, bool decrease, ref HashSet<(int, int)> modifiedVertices)
    {
        (int x, int z) = CoordsToIndices(coords.x, coords.z);
        UpdateHeightAtPoint(x, z, decrease, ref modifiedVertices);
    }


    private void UpdateHeightAtPoint(int x, int z, bool decrease, ref HashSet<(int, int)> modifiedVertices)
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
            houseAtVertex[z, x].DestroyHouse(spawnDestroyedHouse: false);

        if (formations.ContainsKey((x, z)))
        {
            if (formations[(x, z)].ShouldDestroy())
            {
                GameController.Instance.DestroyFormation(formations[(x, z)].gameObject);
                formations.Remove((x, z));
            }
            else
                GameController.Instance.MoveFormation(GetVertexHeightAtIndex(x, z), formations[(x, z)].gameObject);
        }

        if (!modifiedVertices.Contains(WorldMap.GlobalCoordsFromLocal(ChunkIndex, VertexIndicesToCoords(x, z))))
            modifiedVertices.Add(WorldMap.GlobalCoordsFromLocal(ChunkIndex, VertexIndicesToCoords(x, z)));

        //Update neighboring vertices in current chunk
        for (int zOffset = -1; zOffset <= 1; ++zOffset)
        {
            for (int xOffset = -1; xOffset <= 1; ++xOffset)
            {
                if ((xOffset, zOffset) == (0, 0)) continue;

                (int x, int z) neighbor = (x + xOffset, z + zOffset);

                // check if we're out of bounds of the current chunk
                if (neighbor.z >= 0 && neighbor.z < vertices.GetLength(0) && neighbor.x >= 0 && neighbor.x < vertices.GetLength(1))
                {
                    if (Mathf.Abs(GetVertexHeightAtIndex(x, z) - GetVertexHeightAtIndex(neighbor.x, neighbor.z)) > STEP_HEIGHT)
                        UpdateHeightAtPoint(neighbor.x, neighbor.z, decrease, ref modifiedVertices);
                }
                else
                {
                    (int x, int z) coords = VertexIndicesToCoords(x, z);

                    (int x, int z) neighborChunk = (neighbor.x < 0 ? ChunkIndex.x - 1 : neighbor.x >= vertices.GetLength(1) ? ChunkIndex.x + 1 : ChunkIndex.x,
                                                    neighbor.z < 0 ? ChunkIndex.z - 1 : neighbor.z >= vertices.GetLength(0) ? ChunkIndex.z + 1 : ChunkIndex.z);

                    (int, int) neighborCoords = (neighbor.x < 0 ? WIDTH : neighbor.x >= vertices.GetLength(1) ? 0 : coords.x,
                                                 neighbor.z < 0 ? WIDTH : neighbor.z >= vertices.GetLength(0) ? 0 : coords.z);

                    if (neighborChunk.x >= 0 && neighborChunk.z >= 0 && neighborChunk.x < WorldMap.CHUNK_NUMBER && neighborChunk.z < WorldMap.CHUNK_NUMBER &&
                        GetVertexHeightAtIndex(x, z) != WorldMap.Instance.GetHeight(neighborChunk, neighborCoords))
                        WorldMap.Instance.UpdateVertex(neighborChunk, neighborCoords, decrease);
                }
            }
        }

        RecomputeCenters(x, z);
    }


    private void RecomputeCenters(int x, int z)
    {
        for (int center_z = -1; center_z <= 0; ++center_z)
        {
            for (int center_x = -1; center_x <= 0; ++center_x)
            {
                (int x, int z) center = (x + center_x, z + center_z);

                if (center.x >= 0 && center.z >= 0 && center.x < centers.GetLength(1) && center.z < centers.GetLength(0))
                {
                    int prevHeight = GetCenterHeightAtIndex(center.x, center.z);
                    int newHeight = CalculateCenterHeight(center.x, center.z);

                    if (prevHeight == newHeight)
                        continue;

                    foreach (int c in centers[center.z, center.x])
                        meshData.vertices[c].y = newHeight;
                }
            }
        }
    }


    public void SetVertexHeightAtPoint(int x, int z, int height)
    {
        (int x, int z) index = CoordsToIndices(x, z);

        foreach (var v in vertices[index.z, index.x])
            meshData.vertices[v].y = height;

        RecomputeCenters(index.x, index.z);
    }

    #endregion
}
