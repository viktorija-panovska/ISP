using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public class WorldMap : NetworkBehaviour
{
    private struct ChunkUpdate : INetworkSerializable, IEquatable<ChunkUpdate>
    {
        public int X;
        public int Z;

        public ChunkUpdate((int x, int z) chunk)
        {
            X = chunk.x;
            Z = chunk.z;
        }

        public bool Equals(ChunkUpdate other)
            => X == other.X && Z == other.Z;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref X);
            serializer.SerializeValue(ref Z);
        }
    }

    private struct VertexUpdate : INetworkSerializable, IEquatable<VertexUpdate>
    {
        public int X;
        public int Z;
        public int Height;

        public VertexUpdate((int x, int z) vertex, int height)
        {
            X = vertex.x;
            Z = vertex.z;
            Height = height;
        }

        public readonly bool Equals(VertexUpdate other)
            => X == other.X && Z == other.Z && Height == other.Height;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref X);
            serializer.SerializeValue(ref Z);
            serializer.SerializeValue(ref Height);
        }
    }



    public static WorldMap Instance { get; private set; }

    public Vector3 Position { get => transform.position; }
    public const int CHUNK_NUMBER = 5;
    public const int WIDTH = CHUNK_NUMBER * Chunk.WIDTH;
    public const int TILE_NUMBER = CHUNK_NUMBER * Chunk.TILE_NUMBER;

    private readonly Chunk[,] chunkMap = new Chunk[CHUNK_NUMBER, CHUNK_NUMBER];
    private HashSet<(int, int)> modifiedChunks = new();
    private HashSet<(int, int)> modifiedVertices = new();

    public int MapSeed;
    public Material WorldMaterial;



    #region Chunks

    public Chunk GetChunk(int x, int z) => chunkMap[z, x];

    public (int, int) GetChunkIndex(float x, float z)
    {
        int chunk_x = Mathf.FloorToInt(x / Chunk.WIDTH);
        int chunk_z = Mathf.FloorToInt(z / Chunk.WIDTH);

        if (chunk_x == CHUNK_NUMBER) chunk_x--;
        if (chunk_z == CHUNK_NUMBER) chunk_z--;

        return (chunk_x, chunk_z);
    }

    #endregion



    #region Coordinate Transformations

    public static (int, int) LocalCoordsFromGlobal(int global_x, int global_z)
    {
        int local_x = global_x % Chunk.WIDTH;
        int local_z = global_z % Chunk.WIDTH;

        if (global_x == WIDTH) local_x = Chunk.WIDTH;
        if (global_z == WIDTH) local_z = Chunk.WIDTH;

        return (local_x, local_z);
    }

    public static (int, int) GlobalCoordsFromLocal((int x, int z) chunkIndex, (int x, int z) local)
        => (local.x + (chunkIndex.x * Chunk.WIDTH), local.z + (chunkIndex.z * Chunk.WIDTH));

    #endregion



    #region Mesh Height

    public int GetHeight((int x, int z) chunk, (int x, int z) localVertexCoord)
        => chunkMap[chunk.z, chunk.x].GetHeight(localVertexCoord.x, localVertexCoord.z);

    public int GetHeight(WorldLocation globalVertexCoord)
        => GetHeight(globalVertexCoord.X, globalVertexCoord.Z);

    private int GetHeight(int x, int z)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(x, z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(x, z);
        return chunkMap[chunk_z, chunk_x].GetHeight(local_x, local_z);
    }

    #endregion



    #region Houses

    public void SetHouseAtVertex(WorldLocation globalVertexCoord, IHouse house)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        chunkMap[chunk_z, chunk_x].SetHouseAtVertex(local_x, local_z, house);
    }

    public IHouse GetHouseAtVertex(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_z, chunk_x].GetHouseAtVertex(local_x, local_z);
    }

    public bool IsOccupied(WorldLocation vertex) => GetHouseAtVertex(vertex) != null;

    public bool IsSpaceDestroyedHouse(WorldLocation vertex)
    {
        IHouse house = GetHouseAtVertex(vertex);
        return house != null && house.GetType() == typeof(DestroyedHouse);
    }

    public bool IsSpaceActiveHouse(WorldLocation vertex)
    {
        IHouse house = GetHouseAtVertex(vertex);
        return house != null && house.GetType() == typeof(House);
    }

    #endregion



    #region Space Accessibility

    public bool IsSpaceUnderwater(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_z, chunk_x].IsSpaceUnderwater(local_x, local_z);
    }

    public bool IsSpaceForest(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_z, chunk_x].IsSpaceForest(local_x, local_z);
    }

    public bool IsSpaceTree(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_z, chunk_x].IsSpaceTree(local_x, local_z);
    }

    public bool IsSpaceRock(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_z, chunk_x].IsSpaceRock(local_x, local_z);
    }

    public bool IsSpaceSwamp(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_z, chunk_x].IsSpaceSwamp(local_x, local_z);
    }

    public bool IsSpaceAccessible(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return !chunkMap[chunk_z, chunk_x].IsSpaceUnderwater(local_x, local_z) && !chunkMap[chunk_z, chunk_x].IsSpaceForest(local_x, local_z);
    }

    #endregion



    #region Natural Formations

    public void SetFormationAtVertex(WorldLocation globalVertexCoord, NaturalFormation formation)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (int local_x, int local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        chunkMap[chunk_z, chunk_x].SetFormationAtVertex(local_x, local_z, formation);
    }

    public void DestroyUnderwaterFormations()
    {
        for (int z = 0; z < CHUNK_NUMBER; ++z)
            for (int x = 0; x < CHUNK_NUMBER; ++x)
                chunkMap[z, x].DestroyUnderwaterFormations();
    }

    public void DestroyAllFormations()
    {
        for (int z = 0; z < CHUNK_NUMBER; ++z)
            for (int x = 0; x < CHUNK_NUMBER; ++x)
                chunkMap[z, x].DestroyAllFormations();
    }

    #endregion



    #region Create Map

    public override void OnNetworkSpawn()
    {
        Instance = this;

        NoiseGenerator.Initialize(MapSeed);
        GenerateWorldMap();
    }

    private void GenerateWorldMap()
    {
        for (int z = 0; z < CHUNK_NUMBER; ++z)
        {
            for (int x = 0; x < CHUNK_NUMBER; ++x)
            {
                Chunk chunk = new((x, z));
                chunk.SetMesh();
                chunk.gameObject.transform.SetParent(transform);
                chunkMap[z, x] = chunk;
            }
        }
    }

    public Color32 GetColor(int height)
    {
        if (height == 0)
            return Color.blue;

        return Color.green;
    }

    #endregion



    #region Update Map

    public void UpdateMapRegion(List<WorldLocation> targets, bool decrease)
    {
        foreach (WorldLocation target in targets)
            UpdateVertex(target, decrease);

        SynchronizeHeights();
    }


    private void UpdateVertex(WorldLocation location, bool decrease)
    {
        (int x, int z) chunkIndex = GetChunkIndex(location.X, location.Z);
        Chunk chunk = chunkMap[chunkIndex.z, chunkIndex.x];
        (int x, int z) local = LocalCoordsFromGlobal(location.X, location.Z);

        chunk.UpdateHeights(local, decrease, ref modifiedVertices);

        if (!modifiedChunks.Contains(chunkIndex))
            modifiedChunks.Add(chunkIndex);
    }


    public void UpdateVertex((int x, int z) chunkIndex, (int x, int z) vertexCoords, bool decrease)
    {
        chunkMap[chunkIndex.z, chunkIndex.x].UpdateHeights(vertexCoords, decrease, ref modifiedVertices);

        if (!modifiedChunks.Contains(chunkIndex))
            modifiedChunks.Add(chunkIndex);
    }


    private void SynchronizeHeights()
    {
        foreach ((int x, int z) coords in modifiedVertices)
            UpdateHeightClientRpc(new VertexUpdate(coords, GetHeight(coords.x, coords.z)));

        foreach ((int x, int z) in modifiedChunks)
            SetChunkMeshClientRpc(new ChunkUpdate((x, z)));

        modifiedVertices = new();
        modifiedChunks = new();
    }


    [ClientRpc]
    private void SetChunkMeshClientRpc(ChunkUpdate update)
    {
        chunkMap[update.Z, update.X].SetMesh();
    }

    [ClientRpc]
    private void UpdateHeightClientRpc(VertexUpdate update)
    {
        if (!IsHost)
        {
            (int x, int z) chunk = GetChunkIndex(update.X, update.Z);
            (int x, int z) local = LocalCoordsFromGlobal(update.X, update.Z);


            if (local.x == 0 && chunk.x > 0)
                chunkMap[chunk.z, chunk.x - 1].SetVertexHeightAtPoint(Chunk.WIDTH, local.z, update.Height);

            if (local.z == 0 && chunk.z > 0)
                chunkMap[chunk.z - 1, chunk.x].SetVertexHeightAtPoint(local.x, Chunk.WIDTH, update.Height);

            if (local.x == 0 && local.z == 0 && chunk.x > 0 && chunk.z > 0)
                chunkMap[chunk.z - 1, chunk.x - 1].SetVertexHeightAtPoint(Chunk.WIDTH, Chunk.WIDTH, update.Height);

            chunkMap[chunk.z, chunk.x].SetVertexHeightAtPoint(local.x, local.z, update.Height);
        }
    }

    #endregion

}
