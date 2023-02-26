using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;


public struct WorldLocation
{
    public float X { get; }
    public float Z { get; }

    public WorldLocation(float x, float z)
    {
        X = Mathf.Round(x / Chunk.TileWidth) * Chunk.TileWidth;
        Z = Mathf.Round(z / Chunk.TileWidth) * Chunk.TileWidth;
    }

    public List<WorldLocation> GetNeighboringLocations()
    {
        List<WorldLocation> neighbors = new();

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; ++dx)
            {
                if ((dx, dz) != (0, 0))
                {
                    (float x, float z) = (X + dx * Chunk.TileWidth, Z + dz * Chunk.TileWidth);

                    if (x >= 0 && z >= 0 &&
                        x < WorldMap.Width && z < WorldMap.Width)
                        neighbors.Add(new WorldLocation(x, z));
                }
            }
        }

        return neighbors;
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
