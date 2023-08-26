using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;



public interface IHouseType
{
    public int HousePrefab { get; }
    public int MaxCapacity { get; }
    public int MaxHealth { get; }
    public int HealthRegenPerUnit { get; }
    public int ManaGain { get; }
    public int UnitReleaseWait { get; }
    public IUnitType UnitType { get; }
}

public struct DestroyedHouse : IHouseType
{
    public readonly int HousePrefab => 0;
    public readonly int MaxCapacity => 0;
    public readonly int MaxHealth => 0;
    public readonly int HealthRegenPerUnit => 0;
    public readonly int ManaGain => 0;
    public readonly int UnitReleaseWait => 0;
    public readonly IUnitType UnitType => null;
}

public struct Tent : IHouseType
{
    public readonly int HousePrefab => 1;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct Hut : IHouseType
{
    public readonly int HousePrefab => 1;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct WoodHouse : IHouseType
{
    public readonly int HousePrefab => 1;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct StoneHouse : IHouseType
{
    public readonly int HousePrefab => 1;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct Fortress : IHouseType
{
    public readonly int HousePrefab => 1;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct City : IHouseType
{
    public readonly int HousePrefab => 1;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}



public class House : NetworkBehaviour, IPlayerObject
{
    public IHouseType HouseType { get; private set; }
    public bool IsDestroyed { get; private set; }
    public List<WorldLocation> Vertices { get; private set; }

    public Teams Team { get; private set; }
    public int Health { get; private set; }
    public int UnitsInHouse { get; private set; }
    public bool ContainsLeader { get; private set; }

    public Slider HealthBar;
    public Image Fill;
    public GameObject LeaderMarker;

    private Dictionary<WorldLocation, Unit> attackingUnits = new();
    private bool IsUnderAttack { get => attackingUnits.Count > 0; }



    public void Initialize(IHouseType houseType, Teams team, List<WorldLocation> vertices)
    {
        HouseType = houseType;
        IsDestroyed = houseType.GetType() == typeof(DestroyedHouse);

        Team = team;
        Vertices = vertices;
        Health = HouseType.MaxHealth;
    }


    #region House Type

    // in 5x5 area
    public static int CountSurroundingFlatSpaces(WorldLocation center)
    {
        int nearbyHouses = 1;

        bool IsTileFlat(WorldLocation start)
        {
            int occupiedVertices = 0;

            for (int z = 0; z <= 1; ++z)
            {
                for (int x = 0; x <= 1; ++x)
                {
                    WorldLocation vertex = new(start.X + x * Chunk.TILE_WIDTH, start.Z + z * Chunk.TILE_WIDTH);

                    if (!vertex.IsInBounds() || WorldMap.Instance.GetHeight(start) != WorldMap.Instance.GetHeight(vertex) ||
                        !WorldMap.Instance.IsSpaceAccessible(vertex) || WorldMap.Instance.IsSpaceSwamp(vertex))
                        return false;

                    if (WorldMap.Instance.IsOccupied(vertex))
                        occupiedVertices++;
                }
            }

            if (occupiedVertices == 4)
            {
                nearbyHouses++;
                WorldMap.Instance.GetHouseAtVertex(start).UpdateType();
                return false;
            }

            return true;
        }

        int flatTiles = 0;

        for (int z = -2; z <= 2; ++z)
        {
            for (int x = -2; x <= 2; ++x)
            {
                if ((x, z) == (0, 0))
                    continue;

                if (IsTileFlat(new(center.X + x * Chunk.TILE_WIDTH, center.Z + z * Chunk.TILE_WIDTH)))
                    flatTiles++;
            }
        }

        return Mathf.FloorToInt(flatTiles / nearbyHouses);
    }

    public static IHouseType GetHouseType(int flatSpaces)
    {
        int houseIndex = Mathf.FloorToInt((flatSpaces + 1) / 5);

        return houseIndex switch
        {
            0 => new Tent(),
            1 => new Hut(),
            2 => new WoodHouse(),
            3 => new StoneHouse(),
            4 => new Fortress(),
            5 => new City(),
            _ => new DestroyedHouse(),
        };
    }

    public void UpdateType()
    {
        WorldLocation? rootVertex = null;

        foreach (WorldLocation vertex in Vertices)
            if (rootVertex == null || vertex.X < rootVertex.Value.X || (vertex.X == rootVertex.Value.X && vertex.Z < rootVertex.Value.Z))
                rootVertex = vertex;

        HouseType = GetHouseType(CountSurroundingFlatSpaces(rootVertex.Value));
    }

    #endregion



    #region Health Bar

    public void OnMouseEnter()
    {
        if (!IsDestroyed)
            ToggleHealthBarServerRpc(show: true);
    }

    public void OnMouseExit()
    {
        if (!IsDestroyed)
            ToggleHealthBarServerRpc(show: false);
    }


    [ServerRpc(RequireOwnership = false)]
    public void ToggleHealthBarServerRpc(bool show, ServerRpcParams parameters = default)
    {
        ToggleHealthBarClientRpc(show, HouseType.MaxHealth, Health, Team, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { parameters.Receive.SenderClientId }
            }
        });
    }

    [ClientRpc]
    private void ToggleHealthBarClientRpc(bool show, int maxHealth, int currentHealth, Teams team, ClientRpcParams parameters = default)
    {
        if (!show)
        {
            HealthBar.gameObject.SetActive(false);
            return;
        }

        HealthBar.maxValue = maxHealth;
        HealthBar.value = currentHealth;
        Fill.color = team == Teams.Red ? Color.red : Color.blue;
        HealthBar.gameObject.SetActive(true);
    }

    [ClientRpc]
    private void UpdateHealthBarClientRpc(int maxHealth, int currentHealth, Teams team, ClientRpcParams parameters = default)
    {
        if (HealthBar.gameObject.activeSelf)
        {
            HealthBar.maxValue = maxHealth;
            HealthBar.value = currentHealth;
            Fill.color = team == Teams.Red ? Color.red : Color.blue;
        }
    }

    #endregion



    #region Units

    public bool CanUnitEnter(Unit unit) => Team == unit.Team && UnitsInHouse < HouseType.MaxCapacity && !IsUnderAttack && unit.OriginHouse != this;

    public void AddUnit()
    {
        UnitsInHouse++;

        // Regenerate house health.
        if (Health + HouseType.HealthRegenPerUnit <= HouseType.MaxHealth)
            Health += HouseType.HealthRegenPerUnit;
        else if (Health < HouseType.MaxHealth)
            Health += HouseType.MaxHealth - Health;

        UpdateHealthBarClientRpc(HouseType.MaxHealth, Health, Team);

        // If this is the first unit in the house, start producing new units.
        if (UnitsInHouse == 1)
            StartCoroutine(ReleaseNewUnits());
    }

    private IEnumerator ReleaseNewUnits()
    {
        while (true)
        {
            yield return new WaitForSeconds(HouseType.UnitReleaseWait);

            if (IsUnderAttack || UnitsInHouse == 0 || GameController.Instance.AreMaxUnitsReached(Team))
                break;

            ReleaseUnit(newUnit: true);
        }
    }

    public void ReleaseAllUnits()
    {
        for (int i = 0; i < UnitsInHouse; ++i)
            ReleaseUnit(newUnit: false);
    }

    private void ReleaseUnit(bool newUnit)
    {
        bool isLeader = false;

        if (!newUnit)
            UnitsInHouse--;

        // The first unit to leave the house will be the leader.
        if (!newUnit && ContainsLeader)
        {
            RemoveLeader();
            isLeader = true;
        }

        // Spawns unit.
        GameController.Instance.SpawnUnit(HouseType.UnitType, Team, Vertices[Random.Range(0, Vertices.Count - 1)], newUnit ? this : null, newUnit, isLeader);

        // Only new units give mana.
        if (newUnit)
        {
            GameController.Instance.AddManaClientRpc(HouseType.UnitType.ManaGain, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { (ulong)Team - 1 }
                }
            });
        }
    }

    #endregion



    #region Battle

    public bool IsAttackable(Teams otherTeam)
        => !IsDestroyed && ((Team == Teams.Red && otherTeam == Teams.Blue) || (Team == Teams.Blue && otherTeam == Teams.Red)) && attackingUnits.Count <= Vertices.Count;

    public List<WorldLocation> GetAttackableVertices()
    {
        List<WorldLocation> available = new();

        foreach (WorldLocation vertex in Vertices)
            if (!attackingUnits.ContainsKey(vertex))
                available.Add(vertex);

        return available;
    }

    public void TakeDamage(int damage, Unit attacker)
    {
        if (!attackingUnits.ContainsKey(attacker.Location))
            attackingUnits.Add(attacker.Location, attacker);

        Health -= damage;
        UpdateHealthBarClientRpc(HouseType.MaxHealth, Health, Team);

        if (Health <= 0)
        {
            DestroyHouse(true);
            return;
        }

        if (UnitsInHouse > 0)
            ReleaseUnit(newUnit: false);
    }

    public void DestroyHouse(bool spawnDestroyedHouse)
    {
        ReleaseAllUnits();
        GameController.Instance.DestroyHouse(this, spawnDestroyedHouse, attackingUnits);
        attackingUnits = new();
    }

    public void EndAttack(Unit attacker)
    {
        attackingUnits.Remove(attacker.Location);

        if (!IsUnderAttack && UnitsInHouse > 0 && !GameController.Instance.AreMaxUnitsReached(Team))
            StartCoroutine(ReleaseNewUnits());
    }

    #endregion



    #region Leader

    public void MakeLeader()
    {
        ContainsLeader = true;
        SetLeaderMarkerClientRpc(true);
    }

    private void RemoveLeader()
    {
        ContainsLeader = false;
        SetLeaderMarkerClientRpc(false);
    }

    [ClientRpc]
    public void SetLeaderMarkerClientRpc(bool active)
    {
        LeaderMarker.SetActive(active);
    }

    public void ReleaseLeader()
    {
        ReleaseUnit(newUnit: false);
        RemoveLeader();
    }

    #endregion
}
