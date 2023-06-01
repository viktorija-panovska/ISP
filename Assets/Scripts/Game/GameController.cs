using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Collections;


public delegate void NotifySpawnUnit(WorldLocation location, House originHouse, bool newUnit);
public delegate void NotifyAttackUnit(Unit redUnit, Unit blueUnit);

public delegate void NotifyPlaceFound(List<WorldLocation> vertices, Teams team);
public delegate void NotifyEnterHouse(Unit unit, House house);
public delegate void NotifyDestroyHouse(House house, bool hasTerrainChanged, HashSet<Unit> attackingUnits);
public delegate void NotifyAttackHouse(Unit unit, House house);


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

    private readonly int[] unitNumber = { 0, 0 };
    private const int MAX_UNITS_PER_PLAYER = 2;

    private Powers activePower = Powers.MoldTerrain;
    private readonly List<ulong> activeUnits = new();

    private WorldLocation lastClickedVertex = new(-1, -1);    // when guiding followers

    public const int MIN_MANA = 0;
    public const int MAX_MANA = 100;
    public int Mana { get; private set; }
    public readonly int[] PowerCost = { 0, 10 };



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

        for (int i = 0; i < STARTING_UNITS; ++i)
        {
            WorldLocation location = GetRandomWorldLocationInRange(min, max);

            while (occupiedSpots.Contains(location))
                location = GetRandomWorldLocationInRange(min, max);
            occupiedSpots.Add(location);

            SpawnUnit(clientId, location, new BaseUnit());
        }
    }

    private WorldLocation GetRandomWorldLocationInRange(int min, int max)
        => new(Random.Range(min, max), Random.Range(min, max));

    private void SpawnUnit(WorldLocation spawnLocation, House originHouse, bool newUnit)
    {
        if (newUnit && (originHouse == null || unitNumber[(int)originHouse.Team - 1] == MAX_UNITS_PER_PLAYER))
        {
            originHouse.StopReleasingUnits();
            return;
        }

        SpawnUnit((ulong)originHouse.Team - 1, spawnLocation, originHouse.HouseType.UnitType, newUnit);
    }

    private void SpawnUnit(ulong ownerId, WorldLocation spawnLocation, IUnitType unitType, bool newUnit = true)
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
        unit.Initialize(unitType);

        unit.EnterHouse += EnterHouse;
        unit.AttackUnit += AttackUnit;
        unit.AttackHouse += AttackHouse;
        unitObject.GetComponent<UnitMovementHandler>().PlaceFound += SpawnHouse;

        AddUnitClientRpc(unitObject.GetComponent<NetworkObject>().NetworkObjectId, new ClientRpcParams 
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
        RemoveUnitClientRpc(unit.gameObject.GetComponent<NetworkObject>().NetworkObjectId, new ClientRpcParams
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

    public void DestroyHouse(House house, bool hasTerrainMoved, HashSet<Unit> attackingUnits = null)
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

        if (!hasTerrainMoved)
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
        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity) && hud.IsClickable(hitInfo.point))
        {
            Vector3 hitPoint = hitInfo.point;

            if (Input.GetMouseButtonDown(0))
                UpdateMapServerRpc(hitPoint.x, hitPoint.z, decrease: false);
            else if (Input.GetMouseButtonDown(1))
                UpdateMapServerRpc(hitPoint.x, hitPoint.z, decrease: true);
        }            
    }


    [ServerRpc]
    private void UpdateMapServerRpc(float x, float z, bool decrease)
    {
        WorldMap.Instance.UpdateMap(new WorldLocation(x, z), decrease);
    }

    #endregion



    #region Guide Followers
    private void GuideFollowers()
    {
        if (Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo, Mathf.Infinity))
        {
            Vector3 endPoint = hitInfo.point;

            if (hud.IsClickable(endPoint) && Input.GetMouseButtonDown(0))
            {
                RemoveMana(PowerCost[(int)Powers.GuideFollowers]);

                activePower = Powers.MoldTerrain;

                foreach (ulong id in activeUnits)
                    MoveUnitServerRpc(id, endPoint);
            }

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



    #region Earthquake
    private void CauseEarthquake()
    {

    }

    #endregion
}
