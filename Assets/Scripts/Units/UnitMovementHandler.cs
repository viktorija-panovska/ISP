using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum MoveState
{
    Searching,
    FoundFriendlyHouse,
    FoundEnemyHouse,
    FoundFlatSpace,
    Stop
}

public class UnitMovementHandler : NetworkBehaviour
{
    private Unit Unit { get => GetComponent<Unit>(); }

    private Vector3 Position { get => transform.position; set => transform.position = value; }

    private const float MoveSpeed = 2f;
    private const float PositionError = 0.5f;

    private bool isGuided = false;


    // Following path
    private List<WorldLocation> path;
    private int targetIndex = 0;
    private Vector3? pathTarget;
    private bool intermediateStep;


    // Roaming
    private const int ViewDistance = 5;
    private const int RoamDistance = 10;
    private int stepsTaken;
    private (int x, int z) roamDirection;


    // Building
    private MoveState lastMoveState = MoveState.Searching;
    private MoveState moveState = MoveState.Searching;
    private List<WorldLocation> houseVertices;

    public event NotifyPlaceFound PlaceFound;


    private void Start()
    {
        if (!IsOwner) return;


        // Once units are released from a house, don't check the first step and set roaming direction in the opposite direction of the house

        roamDirection = ChooseRoamDirection(new WorldLocation(Position.x, Position.z));
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (moveState == MoveState.Stop)
            return;

        if (path != null)
            FollowPath();
        else
            Roam();
    }

    public void Stop()
    {
        lastMoveState = moveState;
        moveState = MoveState.Stop;
    }

    public void Resume()
    {
        moveState = moveState != MoveState.Stop ? lastMoveState : MoveState.Searching;
    }



    private void EndPath()
    {
        path = null;
        targetIndex = 0;

        if (moveState == MoveState.FoundFlatSpace && IsStillFree())
        {
            moveState = MoveState.Stop;
            OnPlaceFound();
            Unit.OnEnterHouse();
            return;
        }
        else if (moveState == MoveState.FoundFriendlyHouse && IsFriendlyHouse())
        {
            moveState = MoveState.Stop;
            Unit.OnEnterHouse();
            return;
        }
        else if (moveState == MoveState.FoundEnemyHouse && IsEnemyHouse())
        {
            moveState = MoveState.Stop;
            Unit.OnAttackHouse();
            return;
        }
        else
            moveState = MoveState.Searching;
    }

    protected virtual void OnPlaceFound()
    {
        PlaceFound?.Invoke(houseVertices, Unit.Team);
    }

    private bool IsStillFree()
    {
        if (WorldMap.Instance.IsOccupied(houseVertices[0]))
            return false;

        float height = WorldMap.Instance.GetHeight(houseVertices[0]);

        foreach (WorldLocation vertex in houseVertices)
            if (WorldMap.Instance.GetHeight(vertex) != height)
                return false;

        return true;
    }

    private bool IsFriendlyHouse() 
        => WorldMap.Instance.IsOccupied(houseVertices[0]) && WorldMap.Instance.GetHouseAtVertex(houseVertices[0]).IsEnterable(Unit);

    private bool IsEnemyHouse()
        => WorldMap.Instance.IsOccupied(houseVertices[0]) && WorldMap.Instance.GetHouseAtVertex(houseVertices[0]).IsAttackable(Unit.Team);


    #region Following path

    public void SetPath(List<WorldLocation> path, bool isGuided = false)
    {
        this.isGuided = isGuided;
        this.path = path;
        targetIndex = 0;
    }

    private void FollowPath()
    {
        if (pathTarget == null)
        {
            WorldLocation targetLocation = path[targetIndex];
            WorldLocation currentLocation = new(Position.x, Position.z, isCenter: intermediateStep);

            if (!intermediateStep && currentLocation.X != targetLocation.X && currentLocation.Z != targetLocation.Z)
            {
                intermediateStep = true;
                targetLocation = WorldLocation.GetCenter(currentLocation, targetLocation);
            }
            else
                intermediateStep = false;

            pathTarget = new(
                targetLocation.X,
                WorldMap.Instance.GetHeight(targetLocation) + GetComponent<MeshRenderer>().bounds.extents.y,
                targetLocation.Z
            );
        }

        else if (!MoveToTarget(pathTarget.Value))
        {
            pathTarget = null;

            if (!intermediateStep)
            {
                targetIndex++;

                if (targetIndex >= path.Count)
                    EndPath();

                if (!isGuided)
                {
                    if (moveState == MoveState.FoundFlatSpace && !IsStillFree() || 
                        moveState == MoveState.FoundFriendlyHouse && !IsFriendlyHouse() ||
                        moveState == MoveState.FoundEnemyHouse && !IsEnemyHouse())
                    {
                        path = null;
                        targetIndex = 0;
                        moveState = MoveState.Searching;
                        return;
                    }
                }
            }
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

    #endregion


    #region Roaming

    private void Roam()
    {
        WorldLocation currentLocation = new(Position.x, Position.z);

        if (!MoveToHouseOrFree(currentLocation))
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
        WorldLocation targetLocation = new(target.x, target.z);

        if (target.x < 0 || target.z < 0 || target.x > WorldMap.Width || target.z > WorldMap.Width)
        {
            stepsTaken = 0;
            roamDirection = ChooseRoamDirection(currentLocation);
            target = (currentLocation.X + roamDirection.x * Chunk.TileWidth, currentLocation.Z + roamDirection.z * Chunk.TileWidth);
            targetLocation = new WorldLocation(target.x, target.z);
        }

        SetPath(new() { targetLocation });
    }


    private bool MoveToHouseOrFree(WorldLocation currentLocation)
    {
        if (CheckSurroundingSquares(currentLocation))
        {
            EndPath();
            return true;
        }

        WorldLocation? targetLocation;

        if (roamDirection.x == 0)
            targetLocation = FindHouseOrFree_Vertical(currentLocation);
        else if (roamDirection.z == 0)
            targetLocation = FindHouseOrFree_Horizontal(currentLocation);
        else
            targetLocation = FindHouseOrFree_Diagonal(currentLocation);

        if (targetLocation == null)
            return false;

        SetPath(Pathfinding.FindPath(currentLocation, targetLocation.Value));
        return true;
    }

    private WorldLocation? FindHouseOrFree_Vertical(WorldLocation currentLocation)
    {
        for (int dist = 1; dist < ViewDistance; ++dist)
        {
            float targetZ = currentLocation.Z + roamDirection.z * dist * Chunk.TileWidth;

            if (targetZ < 0 || targetZ > WorldMap.Width)
                continue;

            for (int x = -dist; x <= dist + 1; ++x)
            {
                float targetX = currentLocation.X + x * Chunk.TileWidth;

                if (targetX < 0 || targetX > WorldMap.Width)
                    continue;

                WorldLocation target = new(targetX, targetZ);

                if (IsSpaceHouseOrFree(target, roamDirection))
                    return target;
            }
        }

        return null;
    }

    private WorldLocation? FindHouseOrFree_Horizontal(WorldLocation currentLocation)
    {
        for (int dist = 1; dist < ViewDistance; ++dist)
        {
            float targetX = currentLocation.X + roamDirection.x * dist * Chunk.TileWidth;

            if (targetX < 0 || targetX > WorldMap.Width)
                continue;

            for (int z = -dist; z <= dist + 1; ++z)
            {
                float targetZ = currentLocation.Z + z * Chunk.TileWidth;

                if (targetZ < 0 || targetZ > WorldMap.Width)
                    continue;

                WorldLocation target = new(targetX, targetZ);

                if (IsSpaceHouseOrFree(target, roamDirection))
                    return target;
            }
        }

        return null;
    }

    private WorldLocation? FindHouseOrFree_Diagonal(WorldLocation currentLocation)
    {
        for (int dist = 1; dist < ViewDistance; ++dist)
        {
            for (int z = 0; z <= dist; ++z)
            {
                float targetZ = currentLocation.Z + roamDirection.z * z * Chunk.TileWidth;

                if (targetZ < 0 || targetZ > WorldMap.Width)
                    continue;

                if (z == dist)
                {
                    for (int x = 0; x <= dist; ++x)
                    {
                        float targetX = currentLocation.X + roamDirection.z * x * Chunk.TileWidth;

                        if (targetX < 0 || targetX > WorldMap.Width)
                            continue;

                        WorldLocation target = new(targetX, targetZ);

                        if (IsSpaceHouseOrFree(target, roamDirection))
                            return target;
                    }
                }
                else
                {
                    float targetX = currentLocation.X + roamDirection.x * dist * Chunk.TileWidth;

                    if (targetX < 0 || targetX > WorldMap.Width)
                        continue;

                    WorldLocation target = new(targetX, targetZ);

                    if (IsSpaceHouseOrFree(target, roamDirection))
                        return target;
                }
            }
        }
        return null;
    }


    private bool CheckSurroundingSquares(WorldLocation location)
    {
        if (IsSpaceHouseOrFree(location, (1, 0)) || IsSpaceHouseOrFree(location, (-1, 0)))
            return true;

        if (location.Z + Chunk.TileWidth <= WorldMap.Width)
        {
            WorldLocation neighbor = new(location.X, location.Z + Chunk.TileWidth);

            return IsSpaceHouseOrFree(neighbor, (1, 0)) || IsSpaceHouseOrFree(neighbor, (-1, 0));
        }

        return false;
    }

    private bool IsSpaceHouseOrFree(WorldLocation start, (int x, int z) direction)
    {
        (int, int)[] vertexOffsets;
        List<WorldLocation> freeVertices = new() { start };
        List<WorldLocation> occupiedVertices = new();
        bool isEnterable = false;

        if (WorldMap.Instance.IsOccupied(start))
        {
            occupiedVertices.Add(start);
            if (WorldMap.Instance.GetHouseAtVertex(start).IsEnterable(Unit))
                isEnterable = true;
        }

        if (direction.x == 0)
            vertexOffsets = new[] { (0, direction.z), (-1, 0), (-1, direction.z) };
        else if (direction.z == 0)
            vertexOffsets = new[] { (direction.x, 0), (0, -1), (direction.x, -1) };
        else
            vertexOffsets = new[] { (direction.x, 0), (0, direction.z), (direction.x, direction.z) };

        foreach ((int x, int z) in vertexOffsets)
        {
            WorldLocation vertex = new(start.X + x * Chunk.TileWidth, start.Z + z * Chunk.TileWidth);

            if (vertex.X < 0 || vertex.Z < 0 || vertex.X > WorldMap.Width || vertex.Z > WorldMap.Width ||
                WorldMap.Instance.GetHeight(start) != WorldMap.Instance.GetHeight(vertex))
                return false;

            if (WorldMap.Instance.IsOccupied(vertex))
                occupiedVertices.Add(vertex);
            else
                freeVertices.Add(vertex);
        }

        if (occupiedVertices.Count == 4)
        {
            if (isEnterable)
            {
                moveState = MoveState.FoundFriendlyHouse;
                houseVertices = occupiedVertices;
                return true;
            }
            else if (WorldMap.Instance.GetHouseAtVertex(start).IsAttackable(Unit.Team))
            {
                moveState = MoveState.FoundEnemyHouse;
                houseVertices = occupiedVertices;
                return true;
            }
            else return false;
        }
        
        if (freeVertices.Count == 4)
        {
            moveState = MoveState.FoundFlatSpace;
            houseVertices = freeVertices;
            return true;
        }

        return false;
    }
    #endregion
}