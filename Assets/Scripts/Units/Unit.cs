using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;


public enum Team
{
    Red,
    Blue
}

[RequireComponent(typeof(NetworkObject))]
public class Unit
{
    public GameObject UnitObject { get; }
    public WorldLocation PositionInWorldMap;

    public Teams Team { get; private set; }
    public int Health { get; private set; }



    public Unit(GameObject unitObject, WorldLocation worldPosition, Teams team)
    {
        UnitObject = unitObject;
        PositionInWorldMap = worldPosition;
        Team = team;
    }

    //public void MoveUnit(List<WorldLocation> path)
    //{
    //    UnitObject.GetComponent<UnitMovementHandler>().SetPath(path);
    //    PositionInWorldMap = path[^1];
    //}
}
