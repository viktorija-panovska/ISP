using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public class WorldMap : NetworkBehaviour
{
    // World Map
    public Vector3 Position { get => transform.position; }
    public const int ChunkNumber = 2;
    public const int Width = ChunkNumber * Chunk.Width;

    private readonly static Chunk[,] chunkMap = new Chunk[ChunkNumber, ChunkNumber];
    private readonly static List<(int, int)> lastVisibleChunks = new();
    private readonly static List<(int x, int z)> modifiedChunks = new();

    public int MapSeed;
    public Material WorldMaterial;



    public static float GetVertexHeight((int x, int z) chunk, (float x, float z) localVertexCoord)
        => chunkMap[chunk.z, chunk.x].GetVertexHeight(localVertexCoord.x, localVertexCoord.z);

    public static float GetVertexHeight(WorldLocation globalVertexCoord)
    {
        (int chunk_x, int chunk_z) = GetChunkIndex(globalVertexCoord.X, globalVertexCoord.Z);
        (float local_x, float local_z) = LocalCoordsFromGlobal(globalVertexCoord.X, globalVertexCoord.Z);
        return chunkMap[chunk_x, chunk_z].GetVertexHeight(local_x, local_z);
    }

    private static (int, int) GetChunkIndex(float x, float z)
    {
        int chunk_x = Mathf.FloorToInt(x / Chunk.Width);
        int chunk_z = Mathf.FloorToInt(z / Chunk.Width);

        if (chunk_x == ChunkNumber) chunk_x--;
        if (chunk_z == ChunkNumber) chunk_z--;

        return (chunk_x, chunk_z);
    }

    private static (float, float) LocalCoordsFromGlobal(float global_x, float global_z)
    {
        float local_x = global_x % Chunk.Width;
        float local_z = global_z % Chunk.Width;

        if (global_x == Width) local_x = Chunk.Width;
        if (global_z == Width) local_z = Chunk.Width;

        return (local_x, local_z);
    }




    // Create Map
    public override void OnNetworkSpawn()
    {
        NoiseGenerator.Initialize(MapSeed);
        GenerateWorldMap();
    }


    private void GenerateWorldMap()
    {
        for (int z = 0; z < ChunkNumber; ++z)
        {
            for (int x = 0; x < ChunkNumber; ++x)
            {
                Chunk chunk = new Chunk((x, z));
                chunk.gameObject.GetComponent<MeshRenderer>().material = WorldMaterial;
                chunk.SetMesh();
                chunk.gameObject.transform.SetParent(transform);

                chunkMap[z, x] = chunk;
            }
        }
    }


    // Draw Map
    public static void DrawVisibleMap(Vector3 cameraPosition)
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


    // Update Map
    public static void UpdateMap(WorldLocation location, bool decrease)
    {
        (int x, int z) chunkIndex = GetChunkIndex(location.X, location.Z);
        Chunk chunk = chunkMap[chunkIndex.z, chunkIndex.x];

        (float x, float z) local = LocalCoordsFromGlobal(location.X, location.Z);

        chunk.UpdateHeights(local, decrease);
        modifiedChunks.Add(chunkIndex);

        foreach ((int x, int z) in modifiedChunks)
            chunkMap[z, x].SetMesh();
    }

    public static void UpdateVertex((int x, int z) chunk, (float x, float z) vertex, float neighborHeight, bool decrease)
    {
        if (chunk.x >= 0 && chunk.z >= 0 && chunk.x < ChunkNumber && chunk.z < ChunkNumber &&
            chunkMap[chunk.z, chunk.x].GetVertexHeight(vertex.x, vertex.z) != neighborHeight)
        {
            chunkMap[chunk.z, chunk.x].UpdateHeights(vertex, decrease);
            modifiedChunks.Add(chunk);
        }
    }
}
