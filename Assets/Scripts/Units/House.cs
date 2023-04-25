using System.Collections.Generic;
using UnityEngine;

public enum HouseTypes
{
    DestroyedHouse,
    Hut
}

public interface IHouseType
{
    public int MaxCapacity { get; }
    public int MaxHealth { get; }
    public int HealthRegenPerUnit { get; }
    public int ManaGenerated { get; }
}

public struct DestroyedHouse : IHouseType
{
    public int MaxCapacity => 0;
    public int MaxHealth => 0;
    public int HealthRegenPerUnit => 0;
    public int ManaGenerated => 0;
}

public struct Hut : IHouseType
{
    public int MaxCapacity => 2;
    public int MaxHealth => 5;
    public int HealthRegenPerUnit => 2;
    public int ManaGenerated => 2;
}


public class House : MonoBehaviour
{
    public HouseTypes HouseType;
    private IHouseType houseData;
    public Teams Team { get; private set; }
    public List<WorldLocation> OccupiedVertices { get; private set; }

    public int Health { get; private set; }
    public int UnitsInHouse { get; private set; }

    public event NotifyDestroyHouse DestroyHouse;
    public event NotifySpawnUnit ReleaseUnit;

    private bool isUnderAttack = false;


    // House has to produce new units

    public bool IsEnterable(Teams unitTeam) => Team == unitTeam && UnitsInHouse < houseData.MaxCapacity && !isUnderAttack;

    public bool IsAttackable(Teams unitTeam) => (Team == Teams.Red && unitTeam == Teams.Blue) || (Team == Teams.Blue && unitTeam == Teams.Red);



    public void Initialize(Teams team, List<WorldLocation> occupiedVertices)
    {
        houseData = HouseType switch
        {
            HouseTypes.Hut => new Hut(),
            _ => new DestroyedHouse(),
        };

        Team = team;
        OccupiedVertices = occupiedVertices;
        Health = houseData.MaxHealth;
    }


    public void AddUnit()
    {
        UnitsInHouse++;

        if (Health < houseData.MaxHealth)
        {

        }


        // heal the house
    }



    public virtual void OnDestroyHouse()
    {
        DestroyHouse?.Invoke(this);
    }

    public void TakeDamage(int damage)
    {
        Health -= damage;
        isUnderAttack = true;

        //if (UnitsInHouse > 0)
        //{
        //    ReleaseUnit?.Invoke(OccupiedVertices[Random.Range(0, OccupiedVertices.Count - 1)], Team);
        //    UnitsInHouse--;
        //}
    }

    public void EndAttack()
    {
        isUnderAttack = false;
    }
}
