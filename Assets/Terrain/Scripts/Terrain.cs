using System;
using System.Collections.Generic;
using UnityEngine;


public readonly struct MapPoint : IEquatable<MapPoint>
{
    private readonly int m_X;
    private readonly int m_Z;

    public readonly int X { get => m_X; }
    public readonly int Y { get => Terrain.Instance.GetChunkByIndex(m_TouchingChunks[0]).GetVertexHeight(this); }
    public readonly int Z { get => m_Z; }

    private readonly (int x, int z)[] m_TouchingChunks;
    public readonly (int X, int Z)[] TouchingChunks { get => m_TouchingChunks; }


    public MapPoint(float x, float z) 
        : this(Mathf.RoundToInt(x / Terrain.Instance.UnitsPerTile), Mathf.RoundToInt(z / Terrain.Instance.UnitsPerTile)) {}


    public MapPoint(int x, int z)
    {
        m_X = x;
        m_Z = z;

        (int x, int z) mainChunk = (
            m_X == Terrain.Instance.TilesPerSide ? Terrain.Instance.ChunksPerSide - 1 : m_X / Terrain.Instance.TilesPerChunk,
            m_Z == Terrain.Instance.TilesPerSide ? Terrain.Instance.ChunksPerSide - 1 : m_Z / Terrain.Instance.TilesPerChunk
        );

        List<(int x, int z)> chunks = new() { mainChunk };

        Debug.Log($"{x} {z} {Terrain.Instance.GetChunkByIndex(chunks[0])}");

        (int x, int z) pointInChunk = Terrain.Instance.GetChunkByIndex(chunks[0]).GetPointInChunk(x, z);

        // bottom left
        if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == 0 && mainChunk.z > 0)
            chunks.Add((mainChunk.x - 1, mainChunk.z - 1));

        // bottom right
        if (pointInChunk.x == Terrain.Instance.TilesPerChunk && mainChunk.x < Terrain.Instance.ChunksPerSide - 1 && pointInChunk.z == 0 && mainChunk.z > 0)
            chunks.Add((mainChunk.x + 1, mainChunk.z - 1));

        // top left
        if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == Terrain.Instance.TilesPerChunk && mainChunk.z < Terrain.Instance.ChunksPerSide - 1)
            chunks.Add((mainChunk.x - 1, mainChunk.z + 1));

        if (pointInChunk.x == Terrain.Instance.TilesPerChunk && mainChunk.x < Terrain.Instance.ChunksPerSide - 1 &&
            pointInChunk.z == Terrain.Instance.TilesPerChunk && mainChunk.z < Terrain.Instance.ChunksPerSide - 1)
            chunks.Add((mainChunk.x + 1, mainChunk.z + 1));

        // left
        if (pointInChunk.x == 0 && mainChunk.x > 0)
            chunks.Add((mainChunk.x - 1, mainChunk.z));

        // right
        if (pointInChunk.x == Terrain.Instance.TilesPerChunk && mainChunk.x < Terrain.Instance.ChunksPerSide - 1)
            chunks.Add((mainChunk.x + 1, mainChunk.z));

        // bottom
        if (pointInChunk.z == 0 && mainChunk.z > 0)
            chunks.Add((mainChunk.x, mainChunk.z - 1));

        // top
        if (pointInChunk.z == Terrain.Instance.TilesPerChunk && mainChunk.z < Terrain.Instance.ChunksPerSide - 1)
            chunks.Add((mainChunk.x, mainChunk.z + 1));

        m_TouchingChunks = chunks.ToArray();
    }

    public override readonly string ToString() => $"MapPoint -> ({ToVector3()})";

    public readonly Vector3 ToVector3() => new(m_X, 0, m_Z);

    public readonly bool Equals(MapPoint other) => m_X == other.X && m_Z == other.Z;

    public override readonly bool Equals(object obj) => obj.GetType() == typeof(MapPoint) && Equals((MapPoint)obj);

    public override readonly int GetHashCode() => base.GetHashCode();

    public static bool operator ==(MapPoint a, MapPoint b) => a.Equals(b);
    public static bool operator !=(MapPoint a, MapPoint b) => !a.Equals(b);
}


public class Terrain : MonoBehaviour
{
    [SerializeField] private int m_ChunksPerSide = 2;
    [SerializeField] private int m_TilesPerChunk = 5;
    [SerializeField] private int m_UnitsPerTile = 50;
    [SerializeField] private int m_MinHeight = 0;
    [SerializeField] private int m_MaxSteps = 7;
    [SerializeField] private int m_StepHeight = 20;

    [SerializeField] private Material m_TerrainMaterial;
    [SerializeField] private Texture2D m_WaterTexture;
    [SerializeField] private Texture2D m_LandTexture;


    private static Terrain m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static Terrain Instance { get => m_Instance; }

    public int ChunksPerSide { get => m_ChunksPerSide; }
    public int TilesPerSide { get => m_TilesPerChunk * m_ChunksPerSide; }
    public int UnitsPerSide { get => TilesPerSide * m_UnitsPerTile; }
    public int TilesPerChunk { get => m_TilesPerChunk; }
    public int UnitsPerChunk { get => m_UnitsPerTile * m_TilesPerChunk; }
    public int UnitsPerTile { get => m_UnitsPerTile; }

    public int MaxSteps { get => m_MaxSteps; }
    public int MinHeight { get => m_MinHeight; }
    public int MaxHeight { get => m_MaxSteps * m_StepHeight; }
    public int StepHeight { get => m_StepHeight; }

    public Material TerrainMaterial { get => m_TerrainMaterial; }

    private int m_WaterLevel;
    public int WaterLevel { get => m_WaterLevel; }

    private TerrainChunk[,] m_ChunkMap;

    private HashSet<(int, int)> m_ModifiedChunks;


    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
    }

    private void Start()
    {
        m_ChunkMap = new TerrainChunk[m_ChunksPerSide, m_ChunksPerSide];

        HeightMapGenerator.Initialize(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);
        GenerateTerrain();
        GenerateTexture();

        Frame.Instance.SetupFrame();
        CameraController.Instance.UpdateVisibleTerrainChunks();
    }

    #endregion


    #region Terrain Generation

    private void GenerateTerrain()
    {
        for (int z = 0; z < m_ChunksPerSide; ++z)
            for (int x = 0; x < m_ChunksPerSide; ++x)
                m_ChunkMap[z, x] = new(x, z, gameObject.transform);
    }

    private void GenerateTexture()
    {
        m_TerrainMaterial.SetFloat("minHeight", m_MinHeight);
        m_TerrainMaterial.SetFloat("maxHeight", MaxHeight);
        m_TerrainMaterial.SetInt("waterLevel", m_WaterLevel);
        m_TerrainMaterial.SetInt("stepHeight", m_StepHeight);
    }

    #endregion


    #region Getters

    public (int x, int z) GetClosestChunkIndex(Vector3 position)
        => (position.x == UnitsPerSide ? ChunksPerSide - 1 : (int)position.x / UnitsPerChunk, 
            position.z == UnitsPerSide ? ChunksPerSide - 1 : (int)position.z / UnitsPerChunk);

    public TerrainChunk GetChunkByIndex((int x, int z) index)
        => m_ChunkMap[index.z, index.x];

    public bool IsChunkInBounds((int x, int z) chunk)
        => chunk.x >= 0 && chunk.z >= 0 && chunk.x  < ChunksPerSide && chunk.z < ChunksPerSide;

    public bool IsIndexInBounds((int x, int z) index)
        => index.x >= 0 && index.x <= TilesPerSide && index.z >= 0 && index.z <= TilesPerSide;

    public int GetHeightOfPointInChunk((int x, int z) chunk, (int x, int z) point)
        => GetChunkByIndex(chunk).GetVertexHeight(point);

    #endregion


    #region Modify Terrain

    public void ModifyTerrain(MapPoint point, bool lower)
    {
        m_ModifiedChunks = new();
        ChangePointHeight(point, lower);

        foreach ((int, int) chunkIndex in m_ModifiedChunks)
            GetChunkByIndex(chunkIndex).SetMesh();

        m_ModifiedChunks = new();
    }

    public void ChangePointHeight(MapPoint point, bool lower)
    {
        foreach ((int, int) chunkIndex in point.TouchingChunks)
        {
            if (!m_ModifiedChunks.Contains(chunkIndex))
                m_ModifiedChunks.Add(chunkIndex);

            GetChunkByIndex(chunkIndex).ChangePointHeight(point, lower);
        }
    }

    #endregion
}