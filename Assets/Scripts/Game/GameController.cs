using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;



public enum Teams
{
    None,
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
    Flood,
    Armageddon
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

    public static WorldLocation GetRandomWorldLocationInRange(int min, int max)
    {
        WorldLocation randomLocation = new(UnityEngine.Random.Range(min, max), UnityEngine.Random.Range(min, max));

        while (WorldMap.Instance.GetHeight(randomLocation) == 0 || WorldMap.Instance.IsSpaceAccessible(randomLocation))
            randomLocation = new(UnityEngine.Random.Range(min, max), UnityEngine.Random.Range(min, max));

        return randomLocation;
    }

    public bool Equals(WorldLocation other)
        => X == other.X && Z == other.Z;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref X);
        serializer.SerializeValue(ref Z);
    }
}



[RequireComponent(typeof(NetworkObject))]
public class GameController : NetworkBehaviour
{
    public static GameController Instance;

    // Prefabs
    public GameObject WorldMapPrefab;
    public GameObject PlayerControllerPrefab;
    public GameObject[] HousePrefabs;
    public GameObject[] UnitPrefabs;
    public GameObject[] FlagPrefabs;
    public GameObject SwampPrefab;
    public GameObject[] ForestPrefabs;
    public GameObject WaterPlanePrefab;

    // Environment
    private readonly int[] forestDensity = { 4, 1 };
    private GameObject waterPlane;
    public int WaterLevel { get; private set; }

    // Civilization
    private const int STARTING_UNITS = 2;
    private const int MAX_UNITS_PER_PLAYER = 5;
    private readonly List<Unit>[] activeUnits = { new(), new() };
    private readonly int[] units = { 0, 0 };
    private readonly IPlayerObject[] leaders = new IPlayerObject[2];
    private readonly List<House>[] activeHouses = { new(), new() };

    // Powers
    public const int MIN_MANA = 0;
    public const int MAX_MANA = 100;
    public readonly int[] PowerCost = { 0, 0, 0, 0, 0, 0, 0 };

    public const int EARTHQUAKE_RANGE = 3;
    private readonly GameObject[] flags = new GameObject[2];




    #region Setup

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer) 
        { 
            SpawnMap();
            PopulateMap();
        }

        SetupPlayerControllerServerRpc(NetworkManager.Singleton.LocalClientId);
        //InitialSpawnUnitsServerRpc();

        SetupFlagsServerRpc();
    }

    private void SpawnMap()
    {
        GameObject worldMapObject = Instantiate(WorldMapPrefab);
        worldMapObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);

        waterPlane = Instantiate(WaterPlanePrefab);
        waterPlane.transform.position = new(WorldMap.WIDTH / 2, waterPlane.transform.position.y, WorldMap.WIDTH / 2);

        // size the water plane to the size of the map
        Vector3 scale = waterPlane.transform.localScale;
        scale.x = WorldMap.WIDTH * scale.x / waterPlane.GetComponent<Renderer>().bounds.size.x;
        scale.z = WorldMap.WIDTH * scale.z / waterPlane.GetComponent<Renderer>().bounds.size.z;
        waterPlane.transform.localScale = scale;

        waterPlane.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetupPlayerControllerServerRpc(ulong clientID)
    {
        GameObject playerController = Instantiate(PlayerControllerPrefab);
        playerController.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientID, destroyWithScene: true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void InitialSpawnUnitsServerRpc(ServerRpcParams serverRpcParams = default)
    {
        List<WorldLocation> occupiedSpots = new();

        ulong clientId = serverRpcParams.Receive.SenderClientId;

        (int min, int max) = clientId == 0 ? (0, Chunk.WIDTH) : (WorldMap.WIDTH, WorldMap.WIDTH - Chunk.WIDTH);

        int leader = UnityEngine.Random.Range(0, STARTING_UNITS);

        for (int i = 0; i < STARTING_UNITS; ++i)
        {
            WorldLocation location = WorldLocation.GetRandomWorldLocationInRange(min, max);

            while (occupiedSpots.Contains(location))
                location = WorldLocation.GetRandomWorldLocationInRange(min, max);
            occupiedSpots.Add(location);

            SpawnUnit(clientId, location, new BaseUnit(), isLeader: i == leader);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetupFlagsServerRpc()
    {
        for (int i = 0; i < flags.Length; ++i)
        {
            flags[i] = Instantiate(FlagPrefabs[i], new Vector3(0, 0, 0), Quaternion.identity);
            flags[i].GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        }
    }

    #endregion



    [ClientRpc]
    private void AddManaClientRpc(int manaGain, ClientRpcParams clientRpcParams = default)
    {
        PlayerController.Instance.AddMana(manaGain);
    }



    #region Map

    public void MoveFormation(float height, GameObject formation)
    {
        formation.transform.position = new Vector3(formation.transform.position.x, height, formation.transform.position.z);
    }

    public void DestroyFormation(GameObject formation)
    {
        formation.GetComponent<NetworkObject>().Despawn();
    }


    private void PopulateMap()
    {
        for (int z = 0; z < WorldMap.WIDTH; z += Chunk.TILE_WIDTH)
        {
            for (int x = 0; x < WorldMap.WIDTH; x += Chunk.TILE_WIDTH)
            {
                WorldLocation location = new(x, z);

                if (!WorldMap.Instance.IsSpaceAccessible(location))
                    continue;

                for (int i = 0; i < ForestPrefabs.Length; ++i)
                {
                    if (UnityEngine.Random.Range(0, 20) < forestDensity[i])
                    {
                        GameObject forestObject = Instantiate(ForestPrefabs[i],
                            new Vector3(location.X, WorldMap.Instance.GetHeight(location), location.Z),
                            Quaternion.identity);

                        forestObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
                        WorldMap.Instance.SetFormationAtVertex(location, forestObject.GetComponent<NaturalFormation>());

                        break;
                    }
                }
            }
        }
    }

    #endregion



    #region Units

    public void SpawnUnit(WorldLocation spawnLocation, House originHouse, bool newUnit)
    {
        bool isLeader = false;

        if (newUnit && (originHouse == null || activeUnits[(int)originHouse.Team - 1].Count == MAX_UNITS_PER_PLAYER))
        {
            originHouse.StopReleasingUnits();
            return;
        }

        if (!newUnit && originHouse.ContainsLeader)
        {
            originHouse.RemoveLeader();
            isLeader = true;
        }

        SpawnUnit((ulong)originHouse.Team - 1, spawnLocation, originHouse.HouseType.UnitType, newUnit, isLeader);
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
        
        activeUnits[ownerId].Add(unit);
        units[ownerId]++;

        if (isLeader)
        {
            leaders[ownerId] = unit;
            unit.MakeLeader();
        }

        if (newUnit)
        {
            AddManaClientRpc(unitType.ManaGain, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { ownerId }
                }
            });
        }
    }

    public void DespawnUnit(Unit unit, bool isDead = true)
    {
        if (isDead)
        {
            units[(int)unit.Team - 1]--;

            if (unit.IsLeader)
                leaders[(int)unit.Team - 1] = null;
        }

        activeUnits[(int)unit.Team - 1].Remove(unit);
        unit.gameObject.GetComponent<NetworkObject>().Despawn();
    }

    [ServerRpc(RequireOwnership = false)]
    public void AdjustUnitHeightsServerRpc()
    {
        foreach (List<Unit> unitList in activeUnits)
            foreach (Unit unit in unitList)
                unit.UpdateHeight();
    }

    public bool HasLeader(Teams team) => leaders[(int) team - 1] != null;

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

        foreach (WorldLocation vertex in vertices)
            WorldMap.Instance.SetHouseAtVertex(vertex, house);

        if (!house.IsDestroyed)
        {
            activeHouses[(int)team - 1].Add(house);
            AddManaClientRpc(houseType.ManaGain, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { (ulong)team - 1 }
                }
            });
        }
    }

    public void EnterHouse(Unit unit, House house)
    {
        house.AddUnit();

        if (unit.IsLeader)
        {
            leaders[(int)unit.Team - 1] = house;
            house.MakeLeader();
        }

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

        for (int i = 0; i < house.UnitsInHouse; ++i)
            SpawnUnit(house.OccupiedVertices[i % house.OccupiedVertices.Count], house, newUnit: false);

        if (!house.IsDestroyed)
            activeHouses[(int)house.Team - 1].Remove(house);

        house.gameObject.GetComponent<NetworkObject>().Despawn();
    }

    #endregion



    #region Combat

    public void AttackUnit(Unit red, Unit blue)
    {
        StartCoroutine(HitUnit(red, blue));
    }

    private IEnumerator HitUnit(Unit red, Unit blue)
    {
        while (red.Health > 0 && blue.Health > 0)
        {
            int redSpeed = UnityEngine.Random.Range(1, 20) + red.UnitType.Speed;
            int blueSpeed = UnityEngine.Random.Range(1, 20) + blue.UnitType.Speed;

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


    public void AttackHouse(Unit unit, House house)
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



    #region Mold Terrain

    [ServerRpc(RequireOwnership=false)]
    public void UpdateMapServerRpc(WorldLocation location, bool decrease)
    {
        WorldMap.Instance.UpdateMapRegion(new List<WorldLocation> { location }, decrease);
    }

    #endregion



    #region Guide Followers

    [ServerRpc(RequireOwnership = false)]
    public void MoveUnitsServerRpc(Teams team, WorldLocation endLocation)
    {
        int index = (int)team - 1;

        if (flags[index].activeSelf && leaders[index] == null)
            return; 

        flags[index].transform.position = new Vector3(endLocation.X, WorldMap.Instance.GetHeight(endLocation), endLocation.Z);
        flags[index].SetActive(true);

        ReleaseAllUnits(team);
        
        if (leaders[index] == null)
        {
            foreach (Unit unit in activeUnits[index])
            {
                List<WorldLocation> path = Pathfinding.FindPath(new(unit.Position.x, unit.Position.z), endLocation);

                if (path != null && path.Count > 0)
                    unit.MoveUnit(path);
            }
        }
        else
        {
            Unit leader = (Unit)leaders[index];
            List<WorldLocation> path = Pathfinding.FindPath(new(leader.Position.x, leader.Position.z), endLocation);

            if (path != null && path.Count > 0)
                leader.MoveUnit(path);

            foreach (Unit unit in activeUnits[index])
            {
                if (!unit.IsLeader)
                {
                    WorldLocation? next = Pathfinding.FollowUnit(new(unit.Position.x, unit.Position.z), new(leader.Position.x, leader.Position.z));
                    
                    if (next != null)
                        unit.MoveUnit(new List<WorldLocation> { next.Value });
                }
            }
        }
    }

    public void RemoveFlag(Teams team)
    {
        int index = (int)team - 1;
        flags[index].transform.position = new Vector3(0, 0, 0);
        flags[index].SetActive(false);

        EndFollow(team);
    }

    private void EndFollow(Teams team)
    {
        int index = (int)team - 1;

        foreach (Unit unit in activeUnits[index])
            unit.EndFollow();
    }

    private void ReleaseAllUnits(Teams team)
    {
        foreach (House house in activeHouses[(int)team - 1])
            house.ReleaseAllUnits();
    }

    #endregion



    #region Earthquake

    [ServerRpc(RequireOwnership = false)]
    public void LowerTerrainInAreaServerRpc(WorldLocation location)
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

    [ServerRpc(RequireOwnership = false)]
    public void SpawnSwampServerRpc(WorldLocation location)
    {
        House house = WorldMap.Instance.GetHouseAtVertex(location);
        if (house != null)
            DestroyHouse(house, false);

        GameObject swampObject = Instantiate(SwampPrefab, new Vector3(location.X, WorldMap.Instance.GetHeight(location), location.Z), Quaternion.identity);
        WorldMap.Instance.SetFormationAtVertex(location, swampObject.GetComponent<NaturalFormation>());
        swampObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }

    #endregion



    #region Crusade

    [ServerRpc(RequireOwnership = false)]
    public void SendKnightServerRpc(Teams team)
    {
        int index = (int)team - 1;

        if (leaders[index].GetType() == typeof(House))
        {
            House house = (House)leaders[index];
            house.ReleaseLeader();
        }

        Unit leader = (Unit)leaders[index];
        leader.MakeKnight();
        leaders[index] = null;
    }

    #endregion



    #region Flood

    [ServerRpc(RequireOwnership = false)]
    public void IncreaseWaterLevelServerRpc()
    {
        if (WaterLevel == Chunk.MAX_HEIGHT)
            return;

        WaterLevel += Chunk.STEP_HEIGHT;
        waterPlane.transform.position = new(waterPlane.transform.position.x, waterPlane.transform.position.y + Chunk.STEP_HEIGHT, waterPlane.transform.position.z);

        WorldMap.Instance.DestroyUnderwaterFormations();
    }

    #endregion
}
