using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;


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

    public const int MIN_MANA = 0;
    public const int MAX_MANA = 100;

    private const int STARTING_UNITS = 2;
    private const int MAX_UNITS_PER_PLAYER = 5;
    private const int EARTHQUAKE_RANGE = 3;

    public GameObject WorldMapPrefab;
    public GameObject PlayerControllerPrefab;
    public GameObject[] HousePrefabs;
    public GameObject[] UnitPrefabs;
    public GameObject FlagPrefab;
    public GameObject SwampPrefab;

    public readonly int[] PowerCost = { 0, 0, 0, 0 };

    private readonly List<Unit>[] activeUnits = { new(), new() };
    private readonly int[] units = { 0, 0 };



    #region Setup

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer) 
        {
            SpawnMap();
            WorldMap.Instance.SetChunkEvents(MoveSwamp, DestroySwamp);
        }

        SetupPlayerControllerServerRpc(NetworkManager.Singleton.LocalClientId);
        InitialSpawnUnitsServerRpc();
    }

    private void SpawnMap()
    {
        GameObject worldMapObject = Instantiate(WorldMapPrefab);
        worldMapObject.SetActive(true);
        worldMapObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetupPlayerControllerServerRpc(ulong clientID)
    {
        GameObject playerController = Instantiate(PlayerControllerPrefab);
        playerController.SetActive(true);

        NetworkObject networkObject = playerController.GetComponent<NetworkObject>();

        networkObject.SpawnAsPlayerObject(clientID, true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void InitialSpawnUnitsServerRpc(ServerRpcParams serverRpcParams = default)
    {
        List<WorldLocation> occupiedSpots = new();

        ulong clientId = serverRpcParams.Receive.SenderClientId;

        (int min, int max) = clientId == 0 ? (0, Chunk.WIDTH) : (WorldMap.WIDTH, WorldMap.WIDTH - Chunk.WIDTH);

        int leader = UnityEngine.Random.Range(0, STARTING_UNITS - 1);

        for (int i = 0; i < STARTING_UNITS; ++i)
        {
            WorldLocation location = GetRandomWorldLocationInRange(min, max);

            while (occupiedSpots.Contains(location))
                location = GetRandomWorldLocationInRange(min, max);
            occupiedSpots.Add(location);

            SpawnUnit(clientId, location, new BaseUnit(), i == leader);
        }
    }

    #endregion



    [ClientRpc]
    private void AddManaClientRpc(int manaGain, ClientRpcParams clientRpcParams = default)
    {
        PlayerController.Instance.AddMana(manaGain);
    }



    #region Units

    private WorldLocation GetRandomWorldLocationInRange(int min, int max)
    {
        WorldLocation randomLocation = new(UnityEngine.Random.Range(min, max), UnityEngine.Random.Range(min, max));

        while (WorldMap.Instance.GetHeight(randomLocation) == 0)
            randomLocation = new(UnityEngine.Random.Range(min, max), UnityEngine.Random.Range(min, max));

        return randomLocation;
    }

    private void SpawnUnit(WorldLocation spawnLocation, House originHouse, bool newUnit)
    {
        if (newUnit && (originHouse == null || activeUnits[(int)originHouse.Team - 1].Count == MAX_UNITS_PER_PLAYER))
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

        activeUnits[ownerId].Add(unit);
        units[ownerId]++;

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

    private void DespawnUnit(Unit unit, bool isDead = true)
    {
        if (isDead)
            units[(int)unit.Team - 1]--;

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



    #region Mold Terrain

    [ServerRpc(RequireOwnership=false)]
    public void UpdateMapServerRpc(WorldLocation location, bool decrease)
    {
        WorldMap.Instance.UpdateVertex(location, decrease);
    }

    #endregion



    #region Guide Followers

    [ServerRpc(RequireOwnership = false)]
    public void MoveUnitsServerRpc(ulong id, WorldLocation endLocation)
    {
        foreach (Unit unit in activeUnits[id])
        {
            List<WorldLocation> path = Pathfinding.FindPath(new(unit.Position.x, unit.Position.z), endLocation);

            if (path != null && path.Count > 0)
                unit.MoveUnit(path);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnFlagServerRpc(WorldLocation location)
    {
        GameObject flagObject = Instantiate(
            FlagPrefab,
            new Vector3(location.X, WorldMap.Instance.GetHeight(location), location.Z),
            Quaternion.identity);

        flagObject.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveFlagServerRpc()
    {
        Debug.Log("Remove");
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
