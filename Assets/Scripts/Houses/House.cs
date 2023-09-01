using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;


public interface IHouseType
{
    public int Type { get; }
    public int MaxCapacity { get; }
    public int MaxHealth { get; }
    public int HealthRegenPerUnit { get; }
    public int ManaGain { get; }
    public int UnitReleaseWait { get; }
    public IUnitType UnitType { get; }
}

public struct Tent : IHouseType
{
    public readonly int Type => 0;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct Hut : IHouseType
{
    public readonly int Type => 1;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct WoodHouse : IHouseType
{
    public readonly int Type => 2;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct StoneHouse : IHouseType
{
    public readonly int Type => 3;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct Fortress : IHouseType
{
    public readonly int Type => 4;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}

public struct City : IHouseType
{
    public readonly int Type => 5;
    public readonly int MaxCapacity => 2;
    public readonly int MaxHealth => 5;
    public readonly int HealthRegenPerUnit => 2;
    public readonly int ManaGain => 10;
    public readonly int UnitReleaseWait => 10;
    public readonly IUnitType UnitType => new HutUnit();
}


public interface IHouse : IPlayerObject
{
    public List<WorldLocation> Vertices { get; }
    public void DestroyHouse(bool spawnDestroyedHouse);
}


public class House : NetworkBehaviour, IHouse
{
    private MeshFilter Filter { get => GetComponent<MeshFilter>(); }
    private MeshRenderer Renderer { get => GetComponent<MeshRenderer>(); }
    private BoxCollider Collider { get => GetComponent<BoxCollider>(); }

    public Mesh[] HouseMeshes;
    public Material[] HouseMaterials;

    public IHouseType HouseType { get; private set; }
    public List<WorldLocation> Vertices { get; private set; }
    private WorldLocation rootVertex;

    public Teams Team { get; private set; }
    public int Health { get; private set; }
    public int UnitsInHouse { get; private set; }
    public bool ContainsLeader { get; private set; }

    public Slider HealthBar;
    public Image Fill;
    public GameObject LeaderMarker;

    public Dictionary<WorldLocation, Unit> AttackingUnits { get; private set; } = new();
    private bool IsUnderAttack { get => AttackingUnits.Count > 0; }



    public void Initialize(List<WorldLocation> vertices, WorldLocation rootVertex, Teams team)
    {
        Vertices = vertices;
        this.rootVertex = rootVertex;
        Team = team;

        UpdateType();
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
                IHouse ihouse = WorldMap.Instance.GetHouseAtVertex(start);

                if (ihouse.GetType() == typeof(DestroyedHouse))
                    return false;

                nearbyHouses++;

                House house = (House)ihouse;
                house.UpdateType();
                return false;
            }

            return true;
        }

        int flatTiles = 0;

        // null - space has not been checked
        // true - space is flat
        // false - space is not flat
        bool?[,] checkedSpaces = new bool?[5, 5];
        checkedSpaces[2, 2] = true;

        List<(int, int)> spacesToCheck = new();
        spacesToCheck.AddRange(new (int, int)[]{ (1, 0), (0, 1), (-1, 0), (0, -1) });

        for (int i = 0; i < spacesToCheck.Count; ++i)
        {
            (int x, int z) = spacesToCheck[i];

            if (checkedSpaces[2 + x, 2 + z] != null)
                continue;

            if (IsTileFlat(new(center.X + x * Chunk.TILE_WIDTH, center.Z + z * Chunk.TILE_WIDTH)))
            {
                flatTiles++;
                checkedSpaces[2 + x, 2 + z] = true;

                if (x == 0 && Mathf.Abs(z) == 1)
                    spacesToCheck.AddRange(new (int, int)[] { (x, z + z), (x - 1, z), (x + 1, z) });
                else if (Mathf.Abs(x) == 1 && z == 0)
                    spacesToCheck.AddRange(new (int, int)[] { (x + x, z), (x, z - 1), (x, z + 1) });
                else if (Mathf.Abs(x) == 1 && Mathf.Abs(z) == 1)
                    spacesToCheck.AddRange(new (int, int)[] { (x + x, z), (x, z + z) });
                else if (Mathf.Abs(x) == 2 && Mathf.Abs(z) != 2)
                    spacesToCheck.AddRange(new (int, int)[] { (x, z - 1), (x, z + 1) });
                else if (Mathf.Abs(x) != 2 && Mathf.Abs(z) == 2)
                    spacesToCheck.AddRange(new (int, int)[] { (x - 1, z), (x + 1, z) });
            }
            else
                checkedSpaces[2 + x, 2 + z] = false;
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
            _ => new Tent()
        };
    }

    public void UpdateType()
    {
        IHouseType newHouseType = GetHouseType(CountSurroundingFlatSpaces(rootVertex));

        if (HouseType == null || newHouseType.Type != HouseType.Type)
        {
            if (HouseType != null && HouseType.GetType() == typeof(City))
            {
                Vertices = new()
                {
                    new WorldLocation(rootVertex.X + Chunk.TILE_WIDTH, rootVertex.Z + Chunk.TILE_WIDTH),
                    new WorldLocation(rootVertex.X + 2 * Chunk.TILE_WIDTH, rootVertex.Z + Chunk.TILE_WIDTH),
                    new WorldLocation(rootVertex.X + Chunk.TILE_WIDTH, rootVertex.Z + 2 * Chunk.TILE_WIDTH),
                    new WorldLocation(rootVertex.X + 2 * Chunk.TILE_WIDTH, rootVertex.Z + 2 * Chunk.TILE_WIDTH)
                };

                Collider.size = new Vector3(1, 1, 1);
            }

            if (newHouseType.GetType() == typeof(City))
            {
                Vertices = new();
                for (int z = -1; z <= 2; ++z)
                    for (int x = -1; x <= 2; ++x)
                        Vertices.Add(new WorldLocation(rootVertex.X + x * Chunk.TILE_WIDTH, rootVertex.Z + z * Chunk.TILE_WIDTH));

                Collider.size = new Vector3(2, 1, 2);
            }

            HouseType = newHouseType;
            Health = HouseType.MaxHealth;
            Filter.mesh = HouseMeshes[HouseType.Type];
            Renderer.material = HouseMaterials[HouseType.Type];
        }
    }

    #endregion



    #region Health Bar

    public void OnMouseEnter()
    {
        ToggleHealthBarServerRpc(show: true);
    }

    public void OnMouseExit()
    {
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
        => ((Team == Teams.Red && otherTeam == Teams.Blue) || (Team == Teams.Blue && otherTeam == Teams.Red)) && AttackingUnits.Count <= Vertices.Count;

    public List<WorldLocation> GetAttackableVertices()
    {
        List<WorldLocation> available = new();

        foreach (WorldLocation vertex in Vertices)
            if (!AttackingUnits.ContainsKey(vertex))
                available.Add(vertex);

        return available;
    }

    public void TakeDamage(int damage, Unit attacker)
    {
        if (!AttackingUnits.ContainsKey(attacker.Location))
            AttackingUnits.Add(attacker.Location, attacker);

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
        GameController.Instance.DestroyHouse(this, spawnDestroyedHouse);
        AttackingUnits = new();
    }

    public void EndAttack(Unit attacker)
    {
        AttackingUnits.Remove(attacker.Location);

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
