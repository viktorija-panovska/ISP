using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public enum Teams
{
    Red,
    Blue
}

public enum Powers
{
    MoldTerrain,
    GuideFollowers,
    Earthquake,
    Swamp,
    Crusade,
    Volcano,
    Flood,
    Armageddon
}


[RequireComponent(typeof(CameraController))]
public class GameController : NetworkBehaviour
{
    public GameObject CameraRig;
    public Camera PlayerCamera;

    public GameObject RedUnitPrefab;
    public GameObject BlueUnitPrefab;
    private const int startingUnits = 1;

    public Texture2D ClickyCursorTexture;

    private Powers activePower = Powers.MoldTerrain;


    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (IsServer)
            CameraRig.transform.position = new Vector3(0, CameraRig.transform.position.y, 0);
        else
            CameraRig.transform.position = new Vector3(WorldMap.Width, CameraRig.transform.position.y, WorldMap.Width);

        WorldMap.DrawVisibleMap(CameraRig.transform.position);

        if (IsServer)
            SpawnUnitsServerRpc();
    }


    private void Update()
    {
        if (!IsOwner) return;

        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, 0, Screen.height / 2)), out RaycastHit hitInfo, Mathf.Infinity))
            WorldMap.DrawVisibleMap(hitInfo.point);

        if (Input.GetKeyDown(KeyCode.Alpha1))
            activePower = Powers.MoldTerrain;
        if (Input.GetKeyDown(KeyCode.Alpha2))
            activePower = Powers.GuideFollowers;

        switch (activePower)
        {
            case Powers.MoldTerrain:
                MoldTerrain();
                break;

            case Powers.GuideFollowers:
                GuideFollowers();
                break;
        }
    }


    #region Unit Spawn
    [ServerRpc(RequireOwnership = true)]
    private void SpawnUnitsServerRpc()
    {
        List<WorldLocation> occupiedSpots = new();
        
        for (int i = 0; i < startingUnits; ++i)
        {
            // Player 0
            SpawnUnit(RedUnitPrefab, 0, (0, Chunk.Width), ref occupiedSpots);

            // Player 1
            SpawnUnit(BlueUnitPrefab, 1, (WorldMap.Width, WorldMap.Width - Chunk.Width), ref occupiedSpots);
        }
    }


    private WorldLocation GetRandomWorldLocationInRange(int min, int max)
        => new(Random.Range(min, max), Random.Range(min, max));


    private void SpawnUnit(GameObject unitPrefab, ulong ownerId, (int min, int max) spawnLocationRange, ref List<WorldLocation> occupiedSpots)
    {
        WorldLocation location = GetRandomWorldLocationInRange(spawnLocationRange.min, spawnLocationRange.max);

        while (occupiedSpots.Contains(location))
            location = GetRandomWorldLocationInRange(spawnLocationRange.min, spawnLocationRange.max);

        occupiedSpots.Add(location);


        GameObject unit = Instantiate(
            unitPrefab,
            new Vector3(
                location.X, 
                WorldMap.GetVertexHeight(location) + unitPrefab.GetComponent<MeshRenderer>().bounds.size.y / 2, 
                location.Z),
            Quaternion.identity);

        unit.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }
    #endregion


    private bool IsClickable(Vector3 hitPoint)
    {
        if (Mathf.Abs(Mathf.Round(hitPoint.x / Chunk.TileWidth) - hitPoint.x / Chunk.TileWidth) < 0.1 &&
            Mathf.Abs(Mathf.Round(hitPoint.y / Chunk.StepHeight) - hitPoint.y / Chunk.StepHeight) < 0.1 &&
            Mathf.Abs(Mathf.Round(hitPoint.z / Chunk.TileWidth) - hitPoint.z / Chunk.TileWidth) < 0.1)
        {
            Cursor.SetCursor(
                ClickyCursorTexture,
                new Vector2(ClickyCursorTexture.width / 2, ClickyCursorTexture.height / 2),
                CursorMode.Auto
            );

            return true;
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return false;
        }
    }


    #region Mold Terrain
    private void MoldTerrain()
    {
        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (IsClickable(hitPoint))
            {
                if (Input.GetMouseButtonDown(0))
                    UpdateMapServerRpc(hitPoint.x, hitPoint.z, decrease: false);
                else if (Input.GetMouseButtonDown(1))
                    UpdateMapServerRpc(hitPoint.x, hitPoint.z, decrease: true);
            }
        }            
    }


    [ServerRpc]
    public void UpdateMapServerRpc(float x, float z, bool decrease)
    {
        WorldMap.UpdateMap(new WorldLocation(x, z), decrease);

        // TODO: Update heights of units standing on the updated vertices

        SynchronizeMapClientRpc();
    }


    [ClientRpc]
    private void SynchronizeMapClientRpc()
    {
        //if (IsHost) return;
        //WorldMap.SynchronizeMap();
    }

    #endregion



    #region Guide Followers
    private void GuideFollowers()
    {
        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (IsClickable(hitPoint) && Input.GetMouseButtonDown(0))
                MoveUnits(hitPoint);
        }
    }


    private void MoveUnits(Vector3 endPoint)
    {
        //WorldLocation endLocation = new(endPoint.x, endPoint.z);

        //foreach (Unit unit in activeUnits)
        //{
        //    List<WorldLocation> path = Pathfinding.FindPath(unit.PositionInWorldMap, endLocation);

        //    if (path != null && path.Count > 0)
        //        unit.MoveUnit(path);
        //}
    }
    #endregion
}
