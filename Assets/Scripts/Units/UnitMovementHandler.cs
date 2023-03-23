using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Transactions;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;


public class UnitMovementHandler : NetworkBehaviour
{
    private Vector3 Position { get => transform.position; set => transform.position = value; }

    private const float MoveSpeed = 2f;
    private const float PositionError = 0.5f;


    // Following path
    private List<WorldLocation> path;
    private int targetIndex = 0;
    private bool hasNewPath;


    // Roaming
    private const int ViewDistance = 5;
    private const int RoamDistance = 10;
    private int stepsTaken;
    private (int x, int z) roamDirection;
    private Vector3 roamTarget;


    private bool stop = false;


    private void Start()
    {
        if (!IsOwner) return;

        roamDirection = ChooseRoamDirection(new WorldLocation(Position.x, Position.z));
        roamTarget = Position;
    }


    private void Update()
    {
        if (!IsOwner) return;

        if (stop) return;

        if (hasNewPath)
            ResetPath(keepPath: true);

        if (path != null)
            FollowPath();
        else
            Roam();
    }


    #region Following path

    public void SetPath(List<WorldLocation> path)
    {
        this.path = path;
        hasNewPath = true;
    }

    private void FollowPath()
    {
        WorldLocation targetLocation = path[targetIndex];

        Vector3 targetCoordinates = new(
            targetLocation.X,
            WorldMap.Instance.GetVertexHeight(targetLocation) + GetComponent<MeshRenderer>().bounds.extents.y,
            targetLocation.Z
        );

        if (!MoveToTarget(targetCoordinates))
        {
            targetIndex++;

            if (targetIndex >= path.Count)
                ResetPath(keepPath: false);
        }
    }

    private bool MoveToTarget(Vector3 target)
    {
        if (Mathf.Abs(Position.x - target.x) > PositionError ||
            Mathf.Abs(Position.y - target.y) > PositionError ||
            Mathf.Abs(Position.z - target.z) > PositionError)
        {
            Position = Vector3.Lerp(Position, target, MoveSpeed * Time.deltaTime);
            return true;
        }
        return false;
    }

    private void ResetPath(bool keepPath)
    {
        if (!keepPath)
        {
            path = null;
            stop = true;
        }
        targetIndex = 0;
        hasNewPath = false;
    }

    #endregion


    #region Roaming

    private void Roam()
    {
        if (!MoveToTarget(roamTarget))
        {
            WorldLocation currentLocation = new(roamTarget.x, roamTarget.z);

            // we didn't find a flat space to go to
            if (!MoveToFlatSpace(currentLocation))
            {
                if (stepsTaken <= RoamDistance)
                    stepsTaken++;
                else
                {
                    stepsTaken = 0;
                    roamDirection = ChooseRoamDirection(currentLocation);
                }

                ChooseNewRoamTarget(currentLocation);
            }
        }
    }

    private (int, int) ChooseRoamDirection(WorldLocation currentLocation)
    {
        List<(int, int)> availableDirections = new();

        for (int dz = -1; dz <= 1; ++dz)
            for (int dx = -1; dx <= 1; ++dx)
                if ((dx, dz) != (0, 0) && (dx, dz) != (-roamDirection.x, -roamDirection.z) &&
                    currentLocation.X + dx >= 0 && currentLocation.Z + dz >= 0 &&
                    currentLocation.X + dx < WorldMap.Width && currentLocation.Z + dz < WorldMap.Width)
                    availableDirections.Add((dx, dz));

        return availableDirections[Random.Range(0, availableDirections.Count - 1)];
    }

    private void ChooseNewRoamTarget(WorldLocation currentLocation)
    {
        (float x, float z) target = (currentLocation.X + roamDirection.x * Chunk.TileWidth, currentLocation.Z + roamDirection.z * Chunk.TileWidth);

        if (target.x < 0 || target.z < 0 || target.x > WorldMap.Width || target.z > WorldMap.Width)
        {
            stepsTaken = 0;
            roamDirection = ChooseRoamDirection(currentLocation);
            target = (currentLocation.X + roamDirection.x * Chunk.TileWidth, currentLocation.Z + roamDirection.z * Chunk.TileWidth);
        }

        WorldLocation targetLocation = new(target.x, target.z);

        roamTarget = new(
            targetLocation.X,
            WorldMap.Instance.GetVertexHeight(targetLocation) + GetComponent<MeshRenderer>().bounds.extents.y,
            targetLocation.Z
        );
    }


    private bool MoveToFlatSpace(WorldLocation currentLocation)
    {
        WorldLocation? targetLocation;

        if (roamDirection.x == 0)
            targetLocation = FindFlatSpace_Vertical(currentLocation);
        else if (roamDirection.z == 0)
            targetLocation = FindFlatSpace_Horizontal(currentLocation);
        else 
            targetLocation = FindFlatSpace_Diagonal(currentLocation);

        if (targetLocation == null)
            return false;

        SetPath(Pathfinding.FindPath(currentLocation, targetLocation.Value));
        return true;
    }

    private WorldLocation? FindFlatSpace_Vertical(WorldLocation currentLocation)
    {
        for (int dist = 1; dist < ViewDistance; ++dist)
        {
            float targetZ = currentLocation.Z + roamDirection.z * dist * Chunk.TileWidth;

            if (targetZ < 0 || targetZ >= WorldMap.Width)
                continue;

            for (int x = -dist; x <= dist + 1; ++x)
            {
                float targetX = currentLocation.X + x * Chunk.TileWidth;

                if (targetX < 0 || targetX >= WorldMap.Width)
                    continue;

                WorldLocation target = new(targetX, targetZ);

                if (IsSpaceFlat(target))
                    return target;
            }
        }

        return null;
    }

    private WorldLocation? FindFlatSpace_Horizontal(WorldLocation currentLocation)
    {
        for (int dist = 1; dist < ViewDistance; ++dist)
        {
            float targetX = currentLocation.X + roamDirection.x * dist * Chunk.TileWidth;

            if (targetX < 0 || targetX >= WorldMap.Width)
                continue;

            for (int z = -dist; z <= dist + 1; ++z)
            {
                float targetZ = currentLocation.Z + z * Chunk.TileWidth;

                if (targetZ < 0 || targetZ >= WorldMap.Width)
                    continue;

                WorldLocation target = new(targetX, targetZ);

                if (IsSpaceFlat(target))
                    return target;
            }
        }

        return null;
    }

    private WorldLocation? FindFlatSpace_Diagonal(WorldLocation currentLocation)
    {
        for (int dist = 1; dist < ViewDistance; ++dist)
        {
            for (int z = 0; z <= dist; ++z)
            {
                float targetZ = currentLocation.Z + roamDirection.z * z * Chunk.TileWidth;

                if (targetZ < 0 || targetZ >= WorldMap.Width)
                    continue;

                if (z == dist)
                {
                    for (int x = 0; x <= dist; ++x)
                    {
                        float targetX = currentLocation.X + roamDirection.z * x * Chunk.TileWidth;

                        if (targetX < 0 || targetX >= WorldMap.Width)
                            continue;

                        WorldLocation target = new(targetX, targetZ);

                        if (IsSpaceFlat(target))
                            return target;
                    }
                }
                else
                {
                    float targetX = currentLocation.X + roamDirection.x * dist * Chunk.TileWidth;

                    if (targetX < 0 || targetX >= WorldMap.Width)
                        continue;

                    WorldLocation target = new(targetX, targetZ);

                    if (IsSpaceFlat(target))
                        return target;
                }
            }
        }
        return null;
    }

    private bool IsSpaceFlat(WorldLocation start)
    {
        (int, int)[] neighborDirections;

        if (roamDirection.x == 0)
            neighborDirections = new[] { (0, roamDirection.z), (-1, 0), (-1, roamDirection.z) };
        else if (roamDirection.z == 0)
            neighborDirections = new[] { (roamDirection.x, 0), (0, -1), (roamDirection.x, -1) };
        else
            neighborDirections = new[] { (roamDirection.x, 0), (0, roamDirection.z), (roamDirection.x, roamDirection.z) };


        foreach ((int x, int z) in neighborDirections) 
        {
            WorldLocation neighbor = new(start.X + x * Chunk.TileWidth, start.Z + z * Chunk.TileWidth);

            if (neighbor.X < 0 || neighbor.Z < 0 || neighbor.X > WorldMap.Width || neighbor.Z > WorldMap.Width)
                return false;

            if (WorldMap.Instance.GetVertexHeight(start) != WorldMap.Instance.GetVertexHeight(neighbor))
        }

        return true;
    }

    #endregion
}
