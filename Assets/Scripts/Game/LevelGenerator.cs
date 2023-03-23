using UnityEngine;
using Unity.Netcode;


public struct WorldLocation
{
    public float X;
    public float Z;

    public WorldLocation(float x, float z)
    {
        X = Mathf.Round(x / Chunk.TileWidth) * Chunk.TileWidth;
        Z = Mathf.Round(z / Chunk.TileWidth) * Chunk.TileWidth;
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