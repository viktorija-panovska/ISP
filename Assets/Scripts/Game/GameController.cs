using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public delegate void NotifyPlaceFound(List<WorldLocation> vertices, Teams team);
public delegate void NotifyEnterHouse(GameObject unitObject, House house);
public delegate void NotifyDestroyHouse(GameObject house);


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
    public Camera PlayerCamera;

    private bool isGamePaused = false;

    public GameObject RedUnitPrefab;
    public GameObject BlueUnitPrefab;
    private const int startingUnits = 2;

    public GameObject HousePrefab;

    public GameObject GameHUDPrefab;
    public Texture2D ClickyCursorTexture;

    private Powers activePower = Powers.MoldTerrain;
    private readonly List<ulong> activeUnits = new();

    private WorldLocation lastClickedVertex = new(-1, -1);    // when guiding followers



    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // Set HUD
        GameObject hud = Instantiate(GameHUDPrefab);
        hud.GetComponent<GameHUD>().SetGameController(this);

        // Set units
        SpawnUnitsServerRpc();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (isGamePaused) return;

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



    public void PauseGame()
    {
        isGamePaused = true;
    }

    public void ResumeGame()
    {
        isGamePaused = false;
    }



    #region Unit Spawn
    [ServerRpc]
    private void SpawnUnitsServerRpc(ServerRpcParams serverRpcParams = default)
    {
        List<WorldLocation> occupiedSpots = new();

        ulong clientId = serverRpcParams.Receive.SenderClientId;

        GameObject unitPrefab = clientId == 0 ? RedUnitPrefab : BlueUnitPrefab;
        (int min, int max) = clientId == 0 ? (0, Chunk.Width) : (WorldMap.Width, WorldMap.Width - Chunk.Width);

        for (int i = 0; i < startingUnits; ++i)
        {
            WorldLocation location = GetRandomWorldLocationInRange(min, max);

            while (occupiedSpots.Contains(location))
                location = GetRandomWorldLocationInRange(min, max);
            occupiedSpots.Add(location);

            SpawnUnit(unitPrefab, clientId, location);
        }
    }

    private WorldLocation GetRandomWorldLocationInRange(int min, int max)
        => new(Random.Range(min, max), Random.Range(min, max));

    private void SpawnUnit(GameObject unitPrefab, ulong ownerId, WorldLocation spawnLocation)
    {
        GameObject unitObject = Instantiate(
            unitPrefab,
            new Vector3(
                spawnLocation.X,
                WorldMap.Instance.GetHeight(spawnLocation) + unitPrefab.GetComponent<MeshRenderer>().bounds.extents.y,
                spawnLocation.Z),
            Quaternion.identity);

        unitObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        unitObject.GetComponent<Unit>().SetTeam(ownerId);
        unitObject.GetComponent<Unit>().EnterHouse += EnterHouse;
        unitObject.GetComponent<UnitMovementHandler>().PlaceFound += SpawnHouse;

        AddUnitClientRpc(unitObject.GetComponent<NetworkObject>().NetworkObjectId, new ClientRpcParams 
        { 
            Send = new ClientRpcSendParams 
            { 
                TargetClientIds = new ulong[] { ownerId } 
            } 
        });
    }

    private void DespawnUnit(GameObject unitObject)
    {
        RemoveUnitClientRpc(unitObject.GetComponent<NetworkObject>().NetworkObjectId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { (ulong) (unitObject.GetComponent<Unit>().Team == Teams.Red ? 0 : 1) }
            }
        });

        unitObject.GetComponent<NetworkObject>().Despawn();
    }

    [ClientRpc]
    private void AddUnitClientRpc(ulong unitId, ClientRpcParams clientRpcParams = default)
    {
        activeUnits.Add(unitId);
    }

    [ClientRpc]
    private void RemoveUnitClientRpc(ulong unitId, ClientRpcParams clientRpcParams = default)
    {
        activeUnits.Remove(unitId);
    }
    #endregion


    #region Houses
    public void SpawnHouse(List<WorldLocation> vertices, Teams team)
    {
        WorldLocation? rootVertex = null;

        foreach (WorldLocation vertex in vertices)
            if (rootVertex == null || vertex.X < rootVertex.Value.X || (vertex.X == rootVertex.Value.X && vertex.Z < rootVertex.Value.Z))
                rootVertex = vertex;

        GameObject houseObject = Instantiate(
            HousePrefab,
            new Vector3(
                rootVertex.Value.X + HousePrefab.GetComponent<MeshRenderer>().bounds.extents.x + 2.5f,
                WorldMap.Instance.GetHeight(rootVertex.Value) + HousePrefab.GetComponent<MeshRenderer>().bounds.extents.y,
                rootVertex.Value.Z + HousePrefab.GetComponent<MeshRenderer>().bounds.extents.z + 2.5f),
            Quaternion.identity);

        houseObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);

        House house = houseObject.GetComponent<House>();
        house.SetTeam(team);
        house.SetOccupyingVertices(vertices);
        house.DestroyHouse += DestroyHouse;

        foreach (WorldLocation vertex in vertices)
            WorldMap.Instance.SetHouseAtVertex(vertex, house);
    }

    public void EnterHouse(GameObject unitObject, House house)
    {
        house.AddUnit();
        DespawnUnit(unitObject);
    }

    public void DestroyHouse(GameObject houseObject)
    {
        House house = houseObject.GetComponent<House>();
        houseObject.GetComponent<NetworkObject>().Despawn();

        foreach (WorldLocation vertex in house.OccupiedVertices)
            WorldMap.Instance.SetHouseAtVertex(vertex, null);

        ulong clientId = (ulong) (house.Team == Teams.Red ? 0 : 1);
        for (int i = 0; i < house.UnitsInHouse; ++i)
            SpawnUnit(clientId == 0 ? RedUnitPrefab : BlueUnitPrefab, clientId, house.OccupiedVertices[0]);
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
    private void UpdateMapServerRpc(float x, float z, bool decrease)
    {
        WorldMap.Instance.UpdateMap(new WorldLocation(x, z), decrease);

        UpdateUnitHeightsClientRpc();
    }

    [ClientRpc]
    private void UpdateUnitHeightsClientRpc()
    {
        //foreach (ulong id in activeUnits)
        //    GetNetworkObject(id).gameObject.GetComponent<Unit>().UpdateHeight();
    }

    #endregion



    #region Guide Followers
    private void GuideFollowers()
    {
        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 endPoint = hitInfo.point;

            if (IsClickable(endPoint) && Input.GetMouseButtonDown(0))
                foreach (ulong id in activeUnits)
                    MoveUnitServerRpc(id, endPoint);
        }
    }


    [ServerRpc]
    private void MoveUnitServerRpc(ulong id, Vector3 endPoint)
    {
        Unit unit = GetNetworkObject(id).gameObject.GetComponent<Unit>();

        WorldLocation endLocation = new(endPoint.x, endPoint.z);

        if (endLocation.X == lastClickedVertex.X && endLocation.Z == lastClickedVertex.Z)
            return;

        lastClickedVertex = endLocation;

        List<WorldLocation> path = Pathfinding.FindPath(new(unit.Position.x, unit.Position.z), endLocation);

        if (path != null && path.Count > 0)
            unit.MoveUnit(path);
    }
    #endregion
}
