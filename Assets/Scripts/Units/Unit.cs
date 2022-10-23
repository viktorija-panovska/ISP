using System.Collections.Generic;
using UnityEngine;


public enum Team
{
    Red,
    Blue
}


public class Unit
{
    public WorldLocation Location;

    public Team Team { get; private set; }
    public int Health { get; private set; }


    public Unit(WorldLocation location, Team team)
    {
        location = Location;
        Team = team;
    }


    public void MoveUnit(List<WorldLocation> points)
    {
        foreach (var loc in points)
            Debug.Log("Chunk: " + loc.Chunk + "X: " + loc.X + "Y: " + loc.Y);
    }
}
