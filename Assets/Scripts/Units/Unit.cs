using UnityEngine;
using System.Collections.Generic;


public enum Team
{
    Red,
    Blue
}


[RequireComponent(typeof(UnitMovementHandler))]
public class Unit : MonoBehaviour
{
    public Vector3 Position { get => gameObject.transform.position; private set => gameObject.transform.position = value; }
    public WorldLocation Location { get => new(Position.x, Position.z); }

    public Teams Team { get; private set; }
    public int Health { get; private set; }


    public void SetTeam(ulong ownerId)
    {
        if (ownerId == 0)
            Team = Teams.Red;
        else
            Team = Teams.Blue;
    }


    public void MoveUnit(List<WorldLocation> path)
    {
        gameObject.GetComponent<UnitMovementHandler>().SetPath(path);
    }

    public void UpdateHeight()
    {
        Position = new Vector3(Position.x, WorldMap.Instance.GetHeight(Location));
    }
}
