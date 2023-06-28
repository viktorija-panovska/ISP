using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;


public delegate void NotifySpawnUnit(WorldLocation location, House originHouse, bool newUnit);
public delegate void NotifyAttackUnit(Unit redUnit, Unit blueUnit);
public delegate void NotifyDestroyUnit(Unit unit, bool isDead);

public delegate void NotifyPlaceFound(List<WorldLocation> vertices, Teams team);
public delegate void NotifyEnterHouse(Unit unit, House house);
public delegate void NotifyDestroyHouse(House house, bool spawnDestroyedHouse, HashSet<Unit> attackingUnits);
public delegate void NotifyAttackHouse(Unit unit, House house);

public delegate void NotifyRemoveFlag();
public delegate void NotifyMoveSwamp(float height, GameObject swamp);
public delegate void NotifyDestroySwamp(GameObject swamp);


public enum Powers
{
    MoldTerrain,
    GuideFollowers,
    Earthquake,
    Swamp,
    Crusade,
    Flood,
    Armageddon
}


[RequireComponent(typeof(NetworkObject))]
public class GameController : NetworkBehaviour
{
    public bool IsGamePaused { get; private set; }

    public GameObject CameraControllerPrefab;
    private CameraController cameraController;
    private Camera playerCamera;

    public GameObject GameHUDPrefab;
    private GameHUD hud;

    public GameObject[] HousePrefabs;
    public GameObject[] UnitPrefabs;
    private const int STARTING_UNITS = 2;

    private readonly List<Unit> activeUnits = new();
    private readonly int[] unitNumber = { 0, 0 };
    private const int MAX_UNITS_PER_PLAYER = 2;

    private WorldLocation lastClickedVertex = new(-1, -1);    // when guiding followers

    public const int MIN_MANA = 0;
    public const int MAX_MANA = 100;
    public int Mana { get; private set; }
    public readonly int[] PowerCost = { 0, 0, 0, 0 };
    private Powers activePower = Powers.MoldTerrain;

    public GameObject FlagPrefab;
    private Unit leader = null;
    private WorldLocation? flagLocation = null;

    private const int EARTHQUAKE_RANGE = 3;
    public GameObject SwampPrefab;



    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // Set camera controller
        cameraController = Instantiate(CameraControllerPrefab).GetComponent<CameraController>();
        playerCamera = cameraController.MainCamera;

        // Set HUD
        GameObject HUD = Instantiate(GameHUDPrefab);

        hud = HUD.GetComponent<GameHUD>();
        hud.SetGameController(this);

        cameraController.SetGameHUD(hud);

        WorldMap.Instance.SetChunkEvents(MoveSwamp, DestroySwamp);

        // Set units
        InitialSpawnUnitsServerRpc();
    }


    private void Update()
    {
        if (!IsOwner) return;

        if (IsGamePaused) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            activePower = Powers.MoldTerrain;
        if (Input.GetKeyDown(KeyCode.Alpha2))
            activePower = Powers.GuideFollowers;
        if (Input.GetKeyDown(KeyCode.Alpha3))
            activePower = Powers.Earthquake;
        if (Input.GetKeyDown(KeyCode.Alpha4))
            activePower = Powers.Swamp;

        if (PowerCost[(int)activePower] > Mana)
            activePower = Powers.MoldTerrain;

        switch (activePower)
        {
            case Powers.MoldTerrain:
                MoldTerrain();
                break;

            case Powers.GuideFollowers:
                GuideFollowers();
                break;

            case Powers.Earthquake:
                CauseEarthquake();
                break;

            case Powers.Swamp:
                PlaceSwamp();
                break;
        }
    }



    public void PauseGame()
    {
        IsGamePaused = true;
    }

    public void ResumeGame()
    {
        IsGamePaused = false;
    }



    #region Units
    [ServerRpc]
    private void InitialSpawnUnitsServerRpc(ServerRpcParams serverRpcParams = default)
    {
        List<WorldLocation> occupiedSpots = new();

        ulong clientId = serverRpcParams.Receive.SenderClientId;

        (int min, int max) = clientId == 0 ? (0, Chunk.WIDTH) : (WorldMap.WIDTH, WorldMap.WIDTH - Chunk.WIDTH);

        int leader = Random.Range(0, STARTING_UNITS - 1);
        
        for (int i = 0; i < STARTING_UNITS; ++i)
        {
            WorldLocation location = GetRandomWorldLocationInRange(min, max);

            while (occupiedSpots.Contains(location))
                location = GetRandomWorldLocationInRange(min, max);
            occupiedSpots.Add(location);

            SpawnUnit(clientId, location, new BaseUnit(), i == leader);
        }
    }

    private WorldLocation GetRandomWorldLocationInRange(int min, int max)
    {
        WorldLocation randomLocation = new(Random.Range(min, max), Random.Range(min, max));

        while (WorldMap.Instance.GetHeight(randomLocation) == 0)
            randomLocation = new(Random.Range(min, max), Random.Range(min, max));

        return randomLocation;
    }

    private void SpawnUnit(WorldLocation spawnLocation, House originHouse, bool newUnit)
    {
        if (newUnit && (originHouse == null || unitNumber[(int)originHouse.Team - 1] == MAX_UNITS_PER_PLAYER))
        {
            originHouse.StopReleasingUnits();
            return;
        }

        SpawnUnit((ulong)originHouse.Team - 1, spawnLocation, originHouse.HouseType.UnitType, newUnit);
    }

    private void SpawnUnit(ulong ownerId, WorldLocation spawnLocation, IUnitType unitType, bool newUnit = true, bool isLeader = false)
    {
        GameObject unitObject = Instantiate(
            UnitPrefabs[ownerId + 1],
            new Vector3(
                spawnLocation.X,
                WorldMap.Instance.GetHeight(spawnLocation) + UnitPrefabs[ownerId + 1].GetComponent<MeshRenderer>().bounds.extents.y,
                spawnLocation.Z),
            Quaternion.identity);

        unitObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);

        Unit unit = unitObject.GetComponent<Unit>();
        unit.Initialize(unitType, isLeader);

        unit.AttackUnit += AttackUnit;
        unit.DestroyUnit += DespawnUnit;
        unit.EnterHouse += EnterHouse;
        unit.AttackHouse += AttackHouse;
        unitObject.GetComponent<UnitMovementHandler>().PlaceFound += SpawnHouse;

        AddUnitClientRpc(unitObject.GetComponent<NetworkObject>().NetworkObjectId, isLeader, new ClientRpcParams 
        { 
            Send = new ClientRpcSendParams 
            { 
                TargetClientIds = new ulong[] { ownerId } 
            } 
        });
        
        if (newUnit)
        {
            unitNumber[ownerId]++;

            AddManaClientRpc(unitType.ManaGain, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { ownerId }
                }
            });
        }
    }

    private void DespawnUnit(Unit unit, bool isDead = true)
    {
        RemoveUnitClientRpc(unit.gameObject.GetComponent<NetworkObject>().NetworkObjectId, unit.IsLeader && isDead, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { (ulong)unit.Team - 1 }
            }
        });

        unit.gameObject.GetComponent<NetworkObject>().Despawn();

        if (isDead)
            unitNumber[(int)unit.Team - 1]--;
    }

    [ClientRpc]
    private void AddUnitClientRpc(ulong unitId, bool isLeader, ClientRpcParams clientRpcParams = default)
    {
        Unit unit = GetNetworkObject(unitId).gameObject.GetComponent<Unit>();

        if (isLeader)
            leader = unit;

        activeUnits.Add(unit);
    }

    [ClientRpc]
    private void RemoveUnitClientRpc(ulong unitId, bool isLeaderDead, ClientRpcParams clientRpcParams = default)
    {
        Unit unit = GetNetworkObject(unitId).gameObject.GetComponent<Unit>();

        if (isLeaderDead)
            leader = null;

        activeUnits.Add(unit);
    }
    #endregion


    #region Houses
    public void SpawnHouse(List<WorldLocation> vertices, Teams team)
    {
        SpawnHouse(new Hut(), vertices, team);
    }

    private void SpawnHouse(IHouseType houseType, List<WorldLocation> vertices, Teams team = Teams.None)
    {
        WorldLocation? rootVertex = null;

        foreach (WorldLocation vertex in vertices)
            if (rootVertex == null || vertex.X < rootVertex.Value.X || (vertex.X == rootVertex.Value.X && vertex.Z < rootVertex.Value.Z))
                rootVertex = vertex;

        GameObject housePrefab = HousePrefabs[houseType.HousePrefab];

        GameObject houseObject = Instantiate(
            housePrefab,
            new Vector3(
                rootVertex.Value.X + housePrefab.GetComponent<MeshRenderer>().bounds.extents.x + 2.5f,
                WorldMap.Instance.GetHeight(rootVertex.Value) + housePrefab.GetComponent<MeshRenderer>().bounds.extents.y,
                rootVertex.Value.Z + housePrefab.GetComponent<MeshRenderer>().bounds.extents.z + 2.5f),
            Quaternion.identity);

        houseObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);

        House house = houseObject.GetComponent<House>();
        house.Initialize(houseType, team, vertices);
        house.DestroyHouse += DestroyHouse;
        house.ReleaseUnit += SpawnUnit;

        foreach (WorldLocation vertex in vertices)
            WorldMap.Instance.SetHouseAtVertex(vertex, house);


        if (!house.IsDestroyed)
            AddManaClientRpc(houseType.ManaGain, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { (ulong)team - 1 }
                }
            });
    }

    public void EnterHouse(Unit unit, House house)
    {
        house.AddUnit();
        DespawnUnit(unit, false);
    }

    public void DestroyHouse(House house, bool spawnDestroyedHouse, HashSet<Unit> attackingUnits = null)
    {
        if (attackingUnits != null)
        {
            foreach (Unit unit in attackingUnits)
            {
                StopCoroutine(HitHouse(unit, house));
                unit.GetComponent<UnitMovementHandler>().Resume();
            }
        }

        foreach (WorldLocation vertex in house.OccupiedVertices)
            WorldMap.Instance.SetHouseAtVertex(vertex, null);

        if (spawnDestroyedHouse)
            SpawnHouse(new DestroyedHouse(), house.OccupiedVertices);

        ulong clientId = (ulong) (house.Team == Teams.Red ? 0 : 1);
        for (int i = 0; i < house.UnitsInHouse; ++i)
            SpawnUnit(clientId, house.OccupiedVertices[i % house.OccupiedVertices.Count], house.HouseType.UnitType, newUnit: false);

        house.gameObject.GetComponent<NetworkObject>().Despawn();
    }
    #endregion


    #region Combat
    private void AttackUnit(Unit red, Unit blue)
    {
        StartCoroutine(HitUnit(red, blue));
    }

    private IEnumerator HitUnit(Unit red, Unit blue)
    {
        while (red.Health > 0 && blue.Health > 0)
        {
            int redSpeed = Random.Range(1, 20) + red.UnitType.Speed;
            int blueSpeed = Random.Range(1, 20) + blue.UnitType.Speed;

            if (redSpeed > blueSpeed)
            {
                if (Kill(red, blue))
                    break;
            }
            else if (blueSpeed > redSpeed)
            {
                if (Kill(blue, red))
                    break;
            }

            yield return new WaitForSeconds(2);
        }
    }

    private bool Kill(Unit first, Unit second)
    {
        second.TakeDamage(first.UnitType.Strength);

        if (second.Health <= 0)
        {
            DespawnUnit(second);
            first.EndBattle();
            return true;
        }

        first.TakeDamage(second.UnitType.Strength);

        if (first.Health <= 0)
        {
            DespawnUnit(first);
            second.EndBattle();
            return true;
        }

        return false;
    }



    private void AttackHouse(Unit unit, House house)
    {
        StartCoroutine(HitHouse(unit, house));
    }

    private IEnumerator HitHouse(Unit unit, House house)
    {
        while (house.Health > 0 && unit.Health > 0 && !unit.IsFighting)
        {
            house.TakeDamage(unit.UnitType.Strength, unit);
            yield return new WaitForSeconds(2);
        }

        if (unit.Health <= 0 || unit.IsFighting)
            house.EndAttack(unit);
    }

    #endregion



    [ClientRpc]
    private void AddManaClientRpc(int manaGain, ClientRpcParams clientRpcParams = default)
    {
        if (Mana == MAX_MANA)
            return;
        else if (Mana + manaGain > MAX_MANA)
            Mana = MAX_MANA;
        else
            Mana += manaGain;

        hud.UpdateManaBar(Mana);
    }

    private void RemoveMana(int manaLoss)
    {
        if (Mana == MIN_MANA)
            return;
        else if (Mana - manaLoss < MIN_MANA)
            Mana = MIN_MANA;
        else
            Mana -= manaLoss;

        hud.UpdateManaBar(Mana);


    }

    public void SwitchCameras(bool isMapCamera)
    {
        if (isMapCamera)
            PauseGame();
        else
            ResumeGame();

        cameraController.SwitchCameras(isMapCamera);
    }



    #region Mold Terrain

    private void MoldTerrain()
    {
        int index = (int)Powers.MoldTerrain;
        hud.SwitchMarker(index);

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);

                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                    UpdateMapServerRpc(location, decrease: false);
                else if (Input.GetMouseButtonDown(1))
                    UpdateMapServerRpc(location, decrease: true);
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index);
            }
        }
    }


    [ServerRpc]
    private void UpdateMapServerRpc(WorldLocation location, bool decrease)
    {
        WorldMap.Instance.UpdateVertex(location, decrease);
    }

    #endregion



    #region Guide Followers

    private void GuideFollowers()
    {
        int index = (int)Powers.GuideFollowers;
        hud.SwitchMarker(index);

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint) /*&& (leader != null || flagLocation != null)*/)
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    RemoveMana(PowerCost[index]);
                    SpawnFlagServerRpc(location);
                    activePower = Powers.MoldTerrain;
                    hud.SwitchMarker(0);

                    foreach (Unit unit in activeUnits)
                        MoveUnitServerRpc(unit.gameObject.GetComponent<NetworkObject>().NetworkObjectId, location);
                }
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index);
            }
        }
    }


    [ServerRpc]
    private void MoveUnitServerRpc(ulong unitId, WorldLocation endLocation)
    {
        Unit unit = GetNetworkObject(unitId).gameObject.GetComponent<Unit>();

        if (endLocation.X == lastClickedVertex.X && endLocation.Z == lastClickedVertex.Z)
            return;

        lastClickedVertex = endLocation;

        List<WorldLocation> path = Pathfinding.FindPath(new(unit.Position.x, unit.Position.z), endLocation);

        if (path != null && path.Count > 0)
            unit.MoveUnit(path);
    }


    [ServerRpc]
    private void SpawnFlagServerRpc(WorldLocation location)
    {
        flagLocation = location;

        GameObject flagObject = Instantiate(
            FlagPrefab,
            new Vector3(flagLocation.Value.X, WorldMap.Instance.GetHeight(flagLocation.Value), flagLocation.Value.Z),
            Quaternion.identity);

        flagObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }


    [ServerRpc]
    private void RemoveFlagServerRpc()
    {
        Debug.Log("Remove");
    }

    #endregion



    #region Earthquake

    private void CauseEarthquake()
    {
        int index = (int)Powers.Earthquake;
        hud.SwitchMarker(index);

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, false);

                if (Input.GetMouseButtonDown(0))
                {
                    LowerTerrainInAreaServerRpc(location);
                    RemoveMana(PowerCost[index]);
                    activePower = Powers.MoldTerrain;
                }
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index, false);
            }
        }
    }


    [ServerRpc]
    private void LowerTerrainInAreaServerRpc(WorldLocation location)
    {
        List<WorldLocation> targets = new();

        for (int dist = 0; dist <= EARTHQUAKE_RANGE; ++dist)
        {
            for (int z = -dist; z <= dist; ++z)
            {
                float targetZ = location.Z + z * Chunk.TILE_WIDTH;
                if (targetZ < 0 || targetZ > WorldMap.WIDTH)
                    continue;

                int[] xs;

                if (z == dist || z == -dist) xs = Enumerable.Range(-dist, 2 * dist + 1).ToArray();
                else xs = new[] { -dist, dist };

                foreach (int x in xs)
                {
                    float targetX = location.X + (x) * Chunk.TILE_WIDTH;
                    if (targetX < 0 || targetX > WorldMap.WIDTH)
                        continue;

                    targets.Add(new(targetX, targetZ));
                }
            }
        }

        WorldMap.Instance.UpdateMapRegion(targets, decrease: true);
    }

    #endregion



    #region Swamp

    private void PlaceSwamp()
    {
        int index = (int)Powers.Swamp;
        hud.SwitchMarker(index);

        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 hitPoint = hitInfo.point;

            if (hud.IsClickable(hitPoint))
            {
                WorldLocation location = new(hitPoint.x, hitPoint.z);
                hud.HighlightMarker(location, index, true);

                if (Input.GetMouseButtonDown(0))
                {
                    RemoveMana(PowerCost[index]);
                    activePower = Powers.MoldTerrain;
                    SpawnSwampServerRpc(location);
                }
            }
            else
            {
                hud.GrayoutMarker(hitPoint, index);
            }
        }
    }

    [ServerRpc]
    private void SpawnSwampServerRpc(WorldLocation location)
    {
        House house = WorldMap.Instance.GetHouseAtVertex(location);
        if (house != null)
            DestroyHouse(house, false);

        GameObject swampObject = Instantiate(SwampPrefab, new Vector3(location.X, WorldMap.Instance.GetHeight(location), location.Z), Quaternion.identity);
        swampObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        WorldMap.Instance.SetSwampAtVertex(location, swampObject);
    }


    private void MoveSwamp(float height, GameObject swamp)
    {
        swamp.transform.position = new Vector3(swamp.transform.position.x, height, swamp.transform.position.z);
    }

    private void DestroySwamp(GameObject swamp)
    {
        swamp.GetComponent<NetworkObject>().Despawn();
    }

    #endregion
}
