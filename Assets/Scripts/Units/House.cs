using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    public int ManaGain => 2;
    public IUnitType UnitType => new HutUnit();
}


public class House : MonoBehaviour
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
    private bool isUnderAttack = false;
    private const int unitReleaseWait = 10;



    public void Initialize(IHouseType houseType, Teams team, List<WorldLocation> occupiedVertices)
    {
        HouseType = houseType;
        IsDestroyed = houseType.GetType() == typeof(DestroyedHouse);

        Team = team;
        OccupiedVertices = occupiedVertices;
        Health = HouseType.MaxHealth;

        if (IsDestroyed)
        {
            HealthBar.maxValue = HouseType.MaxHealth;
            Fill.color = Team == Teams.Red ? Color.red : Color.blue;
            UpdateHealthBar();
        }
    }


    // Health Bar //

    public void UpdateHealthBar()
    {
        HealthBar.value = Health;
    }

    public void OnMouseOver()
    {
        if (IsDestroyed)
            HealthBar.gameObject.SetActive(true);
    }

    public void OnMouseExit()
    {
        if (IsDestroyed)
            HealthBar.gameObject.SetActive(false);
    }


    // Unit Interaction //

    public bool IsEnterable(Unit unit) => Team == unit.Team && UnitsInHouse < HouseType.MaxCapacity && !isUnderAttack;

    public bool IsAttackable(Teams otherTeam) => IsDestroyed && ((Team == Teams.Red && otherTeam == Teams.Blue) || (Team == Teams.Blue && otherTeam == Teams.Red));


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

        UpdateHealthBar();
    }

    private IEnumerator ReleaseNewUnits()
    {
        while (!isUnderAttack && UnitsInHouse > 0 && !maxUnitsReached)
        {
            yield return new WaitForSeconds(unitReleaseWait);
            ReleaseUnit?.Invoke(OccupiedVertices[Random.Range(0, OccupiedVertices.Count - 1)], this, true);
        }
    }

    public void StopReleasingUnits()
    {
        maxUnitsReached = true;
    }


    // Battle //

    public void TakeDamage(int damage)
    {
        Health -= damage;
        UpdateHealthBar();

        isUnderAttack = true;

        if (UnitsInHouse > 0)
        {
            ReleaseUnit?.Invoke(OccupiedVertices[Random.Range(0, OccupiedVertices.Count - 1)], this, newUnit: false);
            UnitsInHouse--;
        }
    }

    public virtual void OnDestroyHouse()
    {
        DestroyHouse?.Invoke(this);
    }

    public void EndAttack()
    {
        isUnderAttack = false;

        if (UnitsInHouse > 0)
            ReleaseNewUnits();
    }
}
