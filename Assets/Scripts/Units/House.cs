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
    public IUnitType UnitType { get; }
}

public struct DestroyedHouse : IHouseType
{
    public int HousePrefab => 0;
    public int MaxCapacity => 0;
    public int MaxHealth => 0;
    public int HealthRegenPerUnit => 0;
    public int ManaGain => 0;
    public IUnitType UnitType => null;
}

public struct Hut : IHouseType
{
    public int HousePrefab => 1;
    public int MaxCapacity => 2;
    public int MaxHealth => 5;
    public int HealthRegenPerUnit => 2;
    public int ManaGain => 10;
    public IUnitType UnitType => new HutUnit();
}


public class House : NetworkBehaviour
{
    public IHouseType HouseType { get; private set; }
    public bool IsDestroyed { get; private set; }
    public Teams Team { get; private set; }
    public List<WorldLocation> OccupiedVertices { get; private set; }

    public int Health { get; private set; }
    public int UnitsInHouse { get; private set; }

    public event NotifyDestroyHouse DestroyHouse;
    public event NotifySpawnUnit ReleaseUnit;

    public Slider HealthBar;
    public Image Fill;

    private bool maxUnitsReached = false;
    private const int UNIT_RELEASE_WAIT = 10;

    private const int MAX_ATTACKING_UNITS = 4;
    private HashSet<Unit> attackingUnits = new();
    private bool IsUnderAttack { get => attackingUnits.Count > 0; }


    public void Initialize(IHouseType houseType, Teams team, List<WorldLocation> occupiedVertices)
    {
        HouseType = houseType;
        IsDestroyed = houseType.GetType() == typeof(DestroyedHouse);

        Team = team;
        OccupiedVertices = occupiedVertices;
        Health = HouseType.MaxHealth;
    }


    // Health Bar //

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
    private void ToggleHealthBarServerRpc(bool show, ServerRpcParams parameters = default)
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



    // Unit Interaction //

    public bool IsEnterable(Unit unit) => Team == unit.Team && UnitsInHouse < HouseType.MaxCapacity && !IsUnderAttack;

    public bool IsAttackable(Teams otherTeam) 
        => !IsDestroyed && ((Team == Teams.Red && otherTeam == Teams.Blue) || (Team == Teams.Blue && otherTeam == Teams.Red)) && attackingUnits.Count <= MAX_ATTACKING_UNITS;


    // Units //

    public void AddUnit()
    {
        UnitsInHouse++;

        //if (UnitsInHouse == 1)
        //    StartCoroutine(ReleaseNewUnits());

        if (Health + HouseType.HealthRegenPerUnit <= HouseType.MaxHealth)
            Health += HouseType.HealthRegenPerUnit;
        else if (Health < HouseType.MaxHealth)
            Health += HouseType.MaxHealth - Health;

        UpdateHealthBarClientRpc(HouseType.MaxHealth, Health, Team);
    }

    private IEnumerator ReleaseNewUnits()
    {
        while (!IsUnderAttack && UnitsInHouse > 0 && !maxUnitsReached)
        {
            yield return new WaitForSeconds(UNIT_RELEASE_WAIT);
            ReleaseUnit?.Invoke(OccupiedVertices[Random.Range(0, OccupiedVertices.Count - 1)], this, true);
        }
    }

    public void StopReleasingUnits()
    {
        maxUnitsReached = true;
    }


    // Battle //

    public void TakeDamage(int damage, Unit attacker)
    {
        if (!attackingUnits.Contains(attacker))
            attackingUnits.Add(attacker);

        Health -= damage;
        UpdateHealthBarClientRpc(HouseType.MaxHealth, Health, Team);

        if (Health <= 0)
        {
            OnDestroyHouse(true);
            return;
        }

        if (UnitsInHouse > 0)
        {
            ReleaseUnit?.Invoke(OccupiedVertices[Random.Range(0, OccupiedVertices.Count - 1)], this, newUnit: false);
            UnitsInHouse--;
        }
    }

    public virtual void OnDestroyHouse(bool spawnDestroyedHouse)
    {
        DestroyHouse?.Invoke(this, spawnDestroyedHouse, attackingUnits);

        attackingUnits = new();
    }

    public void EndAttack(Unit attacker)
    {
        attackingUnits.Remove(attacker);

        if (!IsUnderAttack && UnitsInHouse > 0 && !maxUnitsReached)
            StartCoroutine(ReleaseNewUnits());
    }
}
