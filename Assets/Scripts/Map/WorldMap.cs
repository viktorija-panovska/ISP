using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public struct ChunkUpdate : INetworkSerializable, IEquatable<ChunkUpdate>
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


public struct VertexUpdate : INetworkSerializable, IEquatable<VertexUpdate>
{
    public int ChunkX;
    public int ChunkZ;
    public int X;
    public int Z;
    public float Height;
    public bool IsCenter;

    public VertexUpdate((int x, int z) chunk, (int x, int z) vertex, float height, bool isCenter)
    {
        ChunkX = chunk.x;
        ChunkZ = chunk.z;
        X = vertex.x;
        Z = vertex.z;
        Height = height;
        IsCenter = isCenter;
    }

    public bool Equals(VertexUpdate other)
        => ChunkX == other.ChunkX && ChunkZ == other.ChunkZ && X == other.X && Z == other.Z && Height == other.Height && IsCenter == other.IsCenter;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ChunkX);
        serializer.SerializeValue(ref ChunkZ);
        serializer.SerializeValue(ref X);
        serializer.SerializeValue(ref Z);
        serializer.SerializeValue(ref Height);
        serializer.SerializeValue(ref IsCenter);
    }
}


public class WorldMap : NetworkBehaviour
{
    public static WorldMap Instance;

    // World Map
    public Vector3 Position { get => transform.position; }
    public const int CHUNK_NUMBER = 5;
    public const int WIDTH = CHUNK_NUMBER * Chunk.WIDTH;

    private readonly Chunk[,] chunkMap = new Chunk[CHUNK_NUMBER, CHUNK_NUMBER];
    private readonly List<(int, int)> modifiedChunks = new();

    public int MapSeed;
    public Material WorldMaterial;


    public float GetHeight((int x, int z) chunk, (float x, float z) localVertexCoord)
        => chunkMap[chunk.z, chunk.x].GetHeight(localVertexCoord.x, localVertexCoord.z);

    public float GetHeight(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (float local_x, float local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_x, chunk_z].GetHeight(local_x, local_z);
    }

    public Chunk GetChunk(int x, int z) => chunkMap[z, x];

    public (int, int) GetChunkIndex(float x, float z)
    {
        int chunk_x = Mathf.FloorToInt(x / Chunk.WIDTH);
        int chunk_z = Mathf.FloorToInt(z / Chunk.WIDTH);

        if (chunk_x == CHUNK_NUMBER) chunk_x--;
        if (chunk_z == CHUNK_NUMBER) chunk_z--;

        return (chunk_x, chunk_z);
    }

    private (float, float) LocalCoordsFromGlobal(float global_x, float global_z)
    {
        float local_x = global_x % Chunk.WIDTH;
        float local_z = global_z % Chunk.WIDTH;

        if (global_x == WIDTH) local_x = Chunk.WIDTH;
        if (global_z == WIDTH) local_z = Chunk.WIDTH;

        return (local_x, local_z);
    }

    public void SetHouseAtVertex(WorldLocation globalVertexCoord, House house)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (float local_x, float local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        chunkMap[chunk_x, chunk_z].SetHouseAtVertex(local_x, local_z, house);
    }

    public House GetHouseAtVertex(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (float local_x, float local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_x, chunk_z].GetHouseAtVertex(local_x, local_z);
    }

    public bool IsOccupied(WorldLocation vertex) => GetHouseAtVertex(vertex) != null;



    // Create Map
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
                chunk.gameObject.GetComponent<MeshRenderer>().material = WorldMaterial;
                chunk.SetMesh();
                chunk.gameObject.transform.SetParent(transform);

                chunkMap[z, x] = chunk;
            }
        }
    }


    #region Update Map
    public void UpdateMap(WorldLocation location, bool decrease)
    {
        (int x, int z) chunkIndex = GetChunkIndex(location.X, location.Z);
        Chunk chunk = chunkMap[chunkIndex.z, chunkIndex.x];
        (float x, float z) local = LocalCoordsFromGlobal(location.X, location.Z);


        var updatedVertices = chunk.UpdateHeights(local, decrease);

        foreach ((int x, int z, float height, bool isCenter) in updatedVertices)
            UpdateHeightClientRpc(new VertexUpdate(chunkIndex, (x, z), height, isCenter));


        modifiedChunks.Add(chunkIndex);

        foreach ((int x, int z) in modifiedChunks)
            SetChunkMeshClientRpc(new ChunkUpdate((x, z)));

        modifiedChunks.Clear();
    }

    public void UpdateVertexInChunk((int x, int z) chunkIndex, (float x, float z) vertexCoords, float neighborHeight, bool decrease)
    {
        if (chunkIndex.x >= 0 && chunkIndex.z >= 0 && chunkIndex.x < CHUNK_NUMBER && chunkIndex.z < CHUNK_NUMBER &&
            chunkMap[chunkIndex.z, chunkIndex.x].GetHeight(vertexCoords.x, vertexCoords.z) != neighborHeight)
        {
            var updatedVertices = chunkMap[chunkIndex.z, chunkIndex.x].UpdateHeights(vertexCoords, decrease);

            foreach ((int x, int z, float height, bool isCenter) in updatedVertices)
                UpdateHeightClientRpc(new VertexUpdate(chunkIndex, (x, z), height, isCenter));

            modifiedChunks.Add(chunkIndex);
        }
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
            chunkMap[update.ChunkZ, update.ChunkX].SetVertexHeightAtPoint(update.Z, update.X, update.Height, update.IsCenter);
    }
    #endregion
}
