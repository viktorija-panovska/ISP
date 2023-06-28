using UnityEngine;
using Unity.Netcode;
using System;

public enum Teams
{
    None,
    Red,
    Blue
}

public struct WorldLocation : INetworkSerializable, IEquatable<WorldLocation>
{
    public int X;
    public int Z;

    public WorldLocation(float x, float z, bool isCenter = false)
    {
        if (!isCenter)
        {
            X = Mathf.RoundToInt(x / Chunk.TILE_WIDTH) * Chunk.TILE_WIDTH;
            Z = Mathf.RoundToInt(z / Chunk.TILE_WIDTH) * Chunk.TILE_WIDTH;
        }
        else
        {
            X = Mathf.CeilToInt(x);
            Z = Mathf.CeilToInt(z);
        }
    }

    public static WorldLocation GetCenter(WorldLocation a, WorldLocation b)
    {
        float dx = (b.X - a.X) / Chunk.TILE_WIDTH;
        float dz = (b.Z - a.Z) / Chunk.TILE_WIDTH;

        return new(a.X + dx * (Chunk.TILE_WIDTH / 2), a.Z + dz * (Chunk.TILE_WIDTH / 2), isCenter: true);
    }

    public bool Equals(WorldLocation other)
        => X == other.X && Z == other.Z;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref X);
        serializer.SerializeValue(ref Z);
    }
}


public class LevelGenerator : NetworkBehaviour
{
    public static LevelGenerator Instance;

    public GameObject WorldMapPrefab;
    public GameObject GameControllerPrefab;


    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer)
            SpawnMap();

        SetupGameControllerServerRpc(NetworkManager.Singleton.LocalClientId);
    }


    [ServerRpc(RequireOwnership=false)]
    private void SetupGameControllerServerRpc(ulong clientID)
    {
        GameObject newController = Instantiate(GameControllerPrefab);
        newController.SetActive(true);

        NetworkObject networkObject = newController.GetComponent<NetworkObject>();

        networkObject.SpawnAsPlayerObject(clientID, true);
    }


    private void SpawnMap()
    {
        GameObject worldMapObject = Instantiate(WorldMapPrefab);
        worldMapObject.SetActive(true);
        worldMapObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }
}
