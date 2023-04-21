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
    public const int ChunkNumber = 1;
    public const int Width = ChunkNumber * Chunk.Width;

    private readonly Chunk[,] chunkMap = new Chunk[ChunkNumber, ChunkNumber];
    private readonly List<(int, int)> lastVisibleChunks = new();
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

    private (int, int) GetChunkIndex(float x, float z)
    {
        int chunk_x = Mathf.FloorToInt(x / Chunk.Width);
        int chunk_z = Mathf.FloorToInt(z / Chunk.Width);

        if (chunk_x == ChunkNumber) chunk_x--;
        if (chunk_z == ChunkNumber) chunk_z--;

        return (chunk_x, chunk_z);
    }

    private (float, float) LocalCoordsFromGlobal(float global_x, float global_z)
    {
        float local_x = global_x % Chunk.Width;
        float local_z = global_z % Chunk.Width;

        if (global_x == Width) local_x = Chunk.Width;
        if (global_z == Width) local_z = Chunk.Width;

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



    // Create Map
    public override void OnNetworkSpawn()
    {
        Instance = this;

        NoiseGenerator.Initialize(MapSeed);
        GenerateWorldMap();
    }

    private void GenerateWorldMap()
    {
        for (int z = 0; z < ChunkNumber; ++z)
        {
            for (int x = 0; x < ChunkNumber; ++x)
            {
                Chunk chunk = new((x, z));
                chunk.gameObject.GetComponent<MeshRenderer>().material = WorldMaterial;
                chunk.SetMesh();
                chunk.gameObject.transform.SetParent(transform);

                chunkMap[z, x] = chunk;
            }
        }
    }


    // Draw Map
    public void DrawVisibleMap(Vector3 cameraPosition)
    {
        foreach ((int x, int z) in lastVisibleChunks)
            chunkMap[z, x].SetVisibility(false);

        lastVisibleChunks.Clear();

        (int chunk_x, int chunk_z) = GetChunkIndex(cameraPosition.x, cameraPosition.z);

        int offset = Mathf.FloorToInt(CameraController.ChunksVisible / 2);

        for (int zOffset = -offset; zOffset <= offset; ++zOffset)
        {
            for (int xOffset = -offset; xOffset <= offset; ++xOffset)
            {
                (int x, int z) newChunk = (chunk_x + xOffset, chunk_z + zOffset);
                if (newChunk.x >= 0 && newChunk.z >= 0 &&
                    newChunk.x < chunkMap.GetLength(1) && newChunk.z < chunkMap.GetLength(0))
                {
                    if (chunkMap[newChunk.z, newChunk.x].DistanceFromPoint(cameraPosition) <= CameraController.ViewDistance)
                    {
                        chunkMap[newChunk.z, newChunk.x].SetVisibility(true);
                        lastVisibleChunks.Add(newChunk);
                    }
                }
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
        if (chunkIndex.x >= 0 && chunkIndex.z >= 0 && chunkIndex.x < ChunkNumber && chunkIndex.z < ChunkNumber &&
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
