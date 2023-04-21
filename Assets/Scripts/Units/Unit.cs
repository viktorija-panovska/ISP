using UnityEngine;
using System.Collections.Generic;


[RequireComponent(typeof(UnitMovementHandler))]
public class Unit : MonoBehaviour
{
    private UnitMovementHandler MovementHandler { get => GetComponent<UnitMovementHandler>(); }

    public Vector3 Position { get => gameObject.transform.position; private set => gameObject.transform.position = value; }
    public WorldLocation Location { get => new(Position.x, Position.z); }

    public Teams Team { get; private set; }
    public int Health { get; private set; }
    public int Strength { get; private set; }
    public int Speed { get; private set; }

    public event NotifyEnterHouse EnterHouse;


    public void SetTeam(ulong ownerId)
    {
        if (ownerId == 0)
            Team = Teams.Red;
        else
            Team = Teams.Blue;
    }

    public void MoveUnit(List<WorldLocation> path)
    {
        MovementHandler.SetPath(path);
    }

    public virtual void OnEnterHouse()
    {
        EnterHouse?.Invoke(gameObject, WorldMap.Instance.GetHouseAtVertex(Location));
    }


    public void UpdateHeight()
    {
        Position = new Vector3(Position.x, WorldMap.Instance.GetHeight(Location));
    }


    public void OnTriggerEnter(Collider other)
    {
        Debug.Log("HI");
    }

    // When another hit box overlaps this unit's hit box, stop the unit.
    // Both units roll 1d20 + Speed for which one goes first.
    // Then they hit each other for Strength amount of damage to health.
}
