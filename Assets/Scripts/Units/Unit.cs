using System.Collections.Generic;
using UnityEngine;


public enum Team
{
    Red,
    Blue
}


public class Unit
{
    public GameObject UnitObject { get; private set; }
    public WorldLocation PositionInWorldMap;

    public Team Team { get; private set; }
    public int Health { get; private set; }


    public Unit(GameObject unitObject, WorldLocation worldPosition, Team team)
    {
        UnitObject = unitObject;
        PositionInWorldMap = worldPosition;
        Team = team;
    }

    public void MoveUnit(List<WorldLocation> path)
    {
        UnitObject.GetComponent<UnitMovementHandler>().SetPath(path);
        PositionInWorldMap = path[^1];
    }
}
