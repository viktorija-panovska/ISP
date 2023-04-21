using System.Collections.Generic;
using UnityEngine;


public interface IHouseType
{
    public int MaxCapacity { get; }
    public int Health { get; }
}

public struct Hut : IHouseType
{
    public int MaxCapacity => 2;
    public int Health => 5;
}


public class House : MonoBehaviour
{
    public const int MaxCapacity = 2;
    public int UnitsInHouse { get; private set; }

    public Teams Team { get; private set; }
    public List<WorldLocation> OccupiedVertices { get; private set; }

    public event NotifyDestroyHouse DestroyHouse;

    public bool IsFull() => UnitsInHouse == MaxCapacity;



    public void SetTeam(Teams team)
    {
        Team = team;
    }

    public void SetOccupyingVertices(List<WorldLocation> vertices)
    {
        OccupiedVertices = vertices;
    }

    public void AddUnit()
    {
        UnitsInHouse++;
    }

    public virtual void OnDestroyHouse()
    {
        DestroyHouse?.Invoke(gameObject);
    }
}
