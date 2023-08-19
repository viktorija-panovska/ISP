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
    public int HousePrefab => 0;
    public int MaxCapacity => 0;
    public int MaxHealth => 0;
    public int HealthRegenPerUnit => 0;
    public int ManaGain => 0;
    public int UnitReleaseWait => 0;
    public IUnitType UnitType => null;
}

public struct Hut : IHouseType
{
    public int HousePrefab => 1;
    public int MaxCapacity => 2;
    public int MaxHealth => 5;
    public int HealthRegenPerUnit => 2;
    public int ManaGain => 10;
    public int UnitReleaseWait => 10;
    public IUnitType UnitType => new HutUnit();
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
        GameController.Instance.SpawnUnit(HouseType.UnitType, Team, Vertices[Random.Range(0, Vertices.Count - 1)], newUnit ? this : null, isLeader);

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
