using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
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

    public bool IsInBounds()
        => X >= 0 && Z >= 0 && X <= WorldMap.WIDTH && Z <= WorldMap.WIDTH;

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



[RequireComponent(typeof(NetworkObject))]
public class GameController : NetworkBehaviour
{
    public static GameController Instance;

    // Prefabs
    public GameObject WorldMapPrefab;
    public GameObject PlayerControllerPrefab;
    public GameObject[] UnitPrefabs;
    public GameObject DestroyedHousePrefab;
    public GameObject HousePrefab;
    public GameObject[] FlagPrefabs;
    public GameObject SwampPrefab;
    public GameObject[] ForestPrefabs;
    public GameObject WaterPlanePrefab;

    // Environment
    private readonly int[] forestDensity = { 4, 1 };
    private GameObject waterPlane;
    public int WaterLevel { get; private set; }

    // Civilization
    private const int STARTING_UNITS = 1;
    private const int MAX_UNITS_PER_PLAYER = 1;
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
            SpawnStarterUnits();
        }

        SetupPlayerControllersServerRpc();
    }

    /// <summary>
    /// Creates map object and water plane object and spawns them on the network.
    /// </summary>
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

    /// <summary>
    /// Creates STARTING_UNITS amount of units for each team, in random locations in a given area, and spawns them on the network.
    /// Selects a leader for each team from the spawned units.
    /// </summary>
    private void SpawnStarterUnits()
    {
        List<WorldLocation> occupiedSpots = new();

        for (int i = 0; i <= 1; ++i)
        {
            List<WorldLocation> availableSpots = FindAvailableSpawnLocations(i, ref occupiedSpots);

            int leader = UnityEngine.Random.Range(0, STARTING_UNITS);

            for (int j = 0; j < STARTING_UNITS; ++j)
            {
                WorldLocation location = availableSpots[UnityEngine.Random.Range(0, availableSpots.Count)];
                availableSpots.Remove(location);
                occupiedSpots.Add(location);
                SpawnUnit(new BaseUnit(), i == 0 ? Teams.Red : Teams.Blue, location, isLeader: j == leader);
            }
        }
    }

    private List<WorldLocation> FindAvailableSpawnLocations(int playerId, ref List<WorldLocation> occupiedSpots)
    {
        List<WorldLocation> availableSpots = new();

        for (int dist = 0; dist < WorldMap.CHUNK_NUMBER; ++dist)
        {
            for (int chunk_z = 0; chunk_z <= dist; ++chunk_z)
            {
                (int, int)[] chunks;

                if (chunk_z == dist)
                    chunks = new (int, int)[] { (dist, dist) };
                else
                    chunks = new (int, int)[] { (chunk_z, dist), (dist, chunk_z) };

                foreach ((int x, int z) chunk in chunks)
                {
                    for (int z = 0; z <= Chunk.WIDTH; z += Chunk.TILE_WIDTH)
                    {
                        for (int x = 0; x <= Chunk.WIDTH; x += Chunk.TILE_WIDTH)
                        {
                            WorldLocation location = new(chunk.x * Chunk.WIDTH + x, chunk.z * Chunk.WIDTH + z);

                            if (playerId == 1)
                                location = new(WorldMap.WIDTH - location.X, WorldMap.WIDTH - location.Z);

                            if (!occupiedSpots.Contains(location) && WorldMap.Instance.IsSpaceAccessible(location))
                                availableSpots.Add(location);
                        }
                    }

                    if (availableSpots.Count > 10)
                        return availableSpots;
                }
            }
        }

        return availableSpots.Count > 0 ? availableSpots : null;
    }

    /// <summary>
    /// Creates a player controller for each user, which will serve as the player object.
    /// </summary>
    /// <param name="serverRpcParams">Holds the client ID</param>
    [ServerRpc(RequireOwnership = false)]
    private void SetupPlayerControllersServerRpc(ServerRpcParams serverRpcParams = default)
    {
        GameObject playerController = Instantiate(PlayerControllerPrefab);
        playerController.GetComponent<NetworkObject>().SpawnAsPlayerObject(serverRpcParams.Receive.SenderClientId, destroyWithScene: true);
    }

    #endregion



    #region Map

    /// <summary>
    /// Randomly sets locations on the map to be populated by Tree objects or Rock objects.
    /// Called only once right after the construction of the map.
    /// </summary>
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

    /// <summary>
    /// Changes the y transform position of an object of type NaturalFormation.
    /// </summary>
    /// <param name="height">The new y transform position of the object.</param>
    /// <param name="formation">Game object of the formation.</param>
    public void MoveFormation(float height, GameObject formation)
    {
        formation.transform.position = new Vector3(formation.transform.position.x, height, formation.transform.position.z);
    }

    /// <summary>
    /// Destroys an object of type NaturalFormation and removes it from the network.
    /// </summary>
    /// <param name="formation">Game object of the formation.</param>
    public void DestroyFormation(GameObject formation)
    {
        formation.GetComponent<NetworkObject>().Despawn();
    }

    #endregion



    #region Units

    public void SpawnUnit(IUnitType unitType, Teams team, WorldLocation spawnLocation, House originHouse = null, bool newUnit = true, bool isLeader = false)
    {
        int playerId = (int)team - 1;

        GameObject unitObject = Instantiate(
            UnitPrefabs[playerId + 1],
            new Vector3(
                spawnLocation.X,
                WorldMap.Instance.GetHeight(spawnLocation) + UnitPrefabs[playerId + 1].GetComponent<MeshRenderer>().bounds.extents.y,
                spawnLocation.Z),
            Quaternion.identity);

        unitObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);

        Unit unit = unitObject.GetComponent<Unit>();
        unit.Initialize(unitType, originHouse, isLeader);
        
        activeUnits[playerId].Add(unit);

        if (newUnit)
            units[playerId]++;

        if (isLeader)
        {
            leaders[playerId] = unit;
            unit.MakeLeader();
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

        if (unit.IsFollowed)
            EndFollowForAll(unit.Team);

        if (unit.ChasedBy != null)
            unit.ChasedBy.EndFollow();

        activeUnits[(int)unit.Team - 1].Remove(unit);
        unit.gameObject.GetComponent<NetworkObject>().Despawn();
    }

    private void AdjustUnitHeights()
    {
        foreach (List<Unit> unitList in activeUnits)
            foreach (Unit unit in unitList)
                unit.UpdateHeight();
    }

    public bool HasLeader(Teams team) => leaders[(int)team - 1] != null;

    public bool AreMaxUnitsReached(Teams teams) => units[(int)teams - 1] >= MAX_UNITS_PER_PLAYER;

    #endregion



    #region Houses

    public void SpawnHouse(List<WorldLocation> vertices, Teams team = Teams.None, bool isDestroyedHouse = false)
    {
        GameObject prefab = isDestroyedHouse ? DestroyedHousePrefab : HousePrefab;

        WorldLocation? rootVertex = null;

        foreach (WorldLocation vertex in vertices)
            if (rootVertex == null || vertex.X < rootVertex.Value.X || (vertex.X == rootVertex.Value.X && vertex.Z < rootVertex.Value.Z))
                rootVertex = vertex;

        GameObject houseObject = Instantiate(
            prefab,
            new Vector3(
                rootVertex.Value.X + prefab.GetComponent<MeshRenderer>().bounds.extents.x + 2.5f,
                WorldMap.Instance.GetHeight(rootVertex.Value) + prefab.GetComponent<MeshRenderer>().bounds.extents.y,
                rootVertex.Value.Z + prefab.GetComponent<MeshRenderer>().bounds.extents.z + 2.5f),
            Quaternion.identity);

        houseObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);

        foreach (WorldLocation vertex in vertices)
            WorldMap.Instance.SetHouseAtVertex(vertex, houseObject.GetComponent<IHouse>());

        if (!isDestroyedHouse)
        {
            House house = houseObject.GetComponent<House>();
            house.Initialize(vertices, rootVertex.Value, team);

            activeHouses[(int)team - 1].Add(house);
            AddManaClientRpc(house.HouseType.ManaGain, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { (ulong)team - 1 }
                }
            });
        }
        else
        {
            DestroyedHouse destroyedHouse = houseObject.GetComponent<DestroyedHouse>();
            destroyedHouse.Initialize(vertices);
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

    public void DestroyHouse(IHouse house, bool spawnDestroyedHouse)
    {
        GameObject houseObject;

        if (house.GetType() == typeof(House))
        {
            House activeHouse = (House)house;

            if (activeHouse.AttackingUnits.Count > 0)
            {
                foreach (Unit unit in activeHouse.AttackingUnits.Values)
                {
                    StopCoroutine(HitHouse(unit, activeHouse));
                    unit.GetComponent<UnitMovementHandler>().Resume();
                }
            }

            activeHouses[(int)activeHouse.Team - 1].Remove(activeHouse);
            houseObject = activeHouse.gameObject;
        }
        else
        {
            houseObject = ((DestroyedHouse)house).gameObject;
        }

        foreach (WorldLocation vertex in house.Vertices)
            WorldMap.Instance.SetHouseAtVertex(vertex, null);

        if (spawnDestroyedHouse)
            SpawnHouse(house.Vertices, isDestroyedHouse: true);

        houseObject.GetComponent<NetworkObject>().Despawn();
    }

    private void UpdateHouses()
    {
        foreach (List<House> houseList in activeHouses)
            foreach (House house in houseList)
                house.UpdateType();
    }

    #endregion



    #region Combat

    public void AttackUnit(Unit red, Unit blue)
    {
        StartCoroutine(HitUnit(red, blue));
    }

    private IEnumerator HitUnit(Unit red, Unit blue)
    {
        while (true)
        {
            yield return new WaitForSeconds(2);

            if (red.Health <= 0 || blue.Health <= 0)
                break;

            int redSpeed = UnityEngine.Random.Range(1, 21) + red.UnitType.Speed;
            int blueSpeed = UnityEngine.Random.Range(1, 21) + blue.UnitType.Speed;

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
        while (true)
        {
            yield return new WaitForSeconds(2);

            if (house.Health <= 0 || unit.Health <= 0 || unit.IsFighting)
                break;

            house.TakeDamage(unit.UnitType.Strength, unit);
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
        AdjustUnitHeights();
        UpdateHouses();
    }

    #endregion



    #region Guide Followers

    [ServerRpc(RequireOwnership = false)]
    public void MoveUnitsServerRpc(Teams team, WorldLocation endLocation)
    {
        int index = (int)team - 1;

        if (flags[index] != null && leaders[index] == null)
            return;

        SpawnFlag(team, new Vector3(endLocation.X, WorldMap.Instance.GetHeight(endLocation), endLocation.Z));

        if (leaders[index].GetType() == typeof(House))
        {
            House house = (House)leaders[index];
            house.ReleaseLeader();
        }

        if (leaders[index] == null)
        {
            ReleaseUnitsFromAllHouses(team);

            foreach (Unit unit in activeUnits[index])
            {
                List<WorldLocation> path = Pathfinding.FindPath(new(unit.Position.x, unit.Position.z), endLocation);

                if (path != null && path.Count > 0)
                    unit.MoveAlongPath(path);
            }
        }
        else
        {
            Unit leader = (Unit)leaders[index];
            List<WorldLocation> path = Pathfinding.FindPath(new(leader.Position.x, leader.Position.z), endLocation);

            if (path != null && path.Count > 0)
                leader.MoveAlongPath(path);

            ReleaseUnitsFromAllHouses(team);

            leader.IsFollowed = true;

            foreach (Unit unit in activeUnits[index])
                if (!unit.IsLeader)
                    unit.FollowUnit(leader);
        }
    }

    private void SpawnFlag(Teams team, Vector3 location)
    {
        int index = (int)team - 1;
        flags[index] = Instantiate(FlagPrefabs[index], location, Quaternion.identity);
        flags[index].GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }

    public void RemoveFlag(Teams team)
    {
        int index = (int)team - 1;
        flags[index].GetComponent<NetworkObject>().Despawn();
        flags[index] = null;
    }

    public void EndFollowForAll(Teams team)
    {
        int index = (int)team - 1;

        foreach (Unit unit in activeUnits[index])
            if (!unit.IsLeader)
                unit.EndFollow();

        Unit leader = (Unit)leaders[index];
        leader.IsFollowed = false;
    }

    private void ReleaseUnitsFromAllHouses(Teams team)
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
        AdjustUnitHeights();
    }

    #endregion



    #region Swamp

    [ServerRpc(RequireOwnership = false)]
    public void SpawnSwampServerRpc(WorldLocation location)
    {
        IHouse house = WorldMap.Instance.GetHouseAtVertex(location);
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



    #region Armageddon

    [ServerRpc(RequireOwnership = false)]
    public void StartArmageddonServerRpc()
    {
        foreach (var houseList in activeHouses)
            foreach (House house in houseList)
                house.ReleaseAllUnits();

        foreach (var unitList in activeUnits)
            foreach (Unit unit in unitList)
                unit.MakeBattleUnit();
    }

    #endregion



    [ClientRpc]
    public void AddManaClientRpc(int manaGain, ClientRpcParams clientRpcParams = default)
    {
        PlayerController.Instance.AddMana(manaGain);
    }
}
