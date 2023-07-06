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

    private const float MOVE_SPEED = 2f;
    private const float POSITION_ERROR = 0.5f;

    private bool isGuided = false;


    // Following path
    private List<WorldLocation> path;
    private int targetIndex = 0;
    private Vector3? pathTarget;
    private bool intermediateStep;

    public WorldLocation StartLocation { get; private set; }
    public WorldLocation EndLocation { get; private set; }


    // Roaming
    private const int VIEW_DISTANCE = 5;
    private const int ROAM_DISTANCE = 10;
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

        StartLocation = new WorldLocation(Position.x, Position.z);
        EndLocation = StartLocation;

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

    public Vector3? GetNextLocation() => pathTarget;

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
            EndLocation = path[targetIndex];
            StartLocation = new(Position.x, Position.z, isCenter: intermediateStep);

            if (!intermediateStep && StartLocation.X != EndLocation.X && StartLocation.Z != EndLocation.Z)
            {
                intermediateStep = true;
                EndLocation = WorldLocation.GetCenter(StartLocation, EndLocation);
            }
            else
                intermediateStep = false;

            pathTarget = new(
                EndLocation.X,
                WorldMap.Instance.GetHeight(EndLocation) + GetComponent<MeshRenderer>().bounds.extents.y,
                EndLocation.Z
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
        if (Mathf.Abs(Position.x - target.x) > POSITION_ERROR ||
            Mathf.Abs(Position.y - target.y) > POSITION_ERROR ||
            Mathf.Abs(Position.z - target.z) > POSITION_ERROR)
        {
            Position = Vector3.Lerp(Position, target, MOVE_SPEED * Time.deltaTime);
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
            if (stepsTaken <= ROAM_DISTANCE)
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
        {
            for (int dx = -1; dx <= 1; ++dx)
            {
                WorldLocation newLoc = new(currentLocation.X + dx * Chunk.TILE_WIDTH, currentLocation.Z + dz * Chunk.TILE_WIDTH);

                if ((dx, dz) != (0, 0) && (dx, dz) != (-roamDirection.x, -roamDirection.z) &&
                    newLoc.X >= 0 && newLoc.Z >= 0 && newLoc.X <= WorldMap.WIDTH && newLoc.Z <= WorldMap.WIDTH)
                    availableDirections.Add((dx, dz));
            }
        }

        return availableDirections[Random.Range(0, availableDirections.Count - 1)];
    }

    private void ChooseNewRoamTarget(WorldLocation currentLocation)
    {
        (float x, float z) target = (currentLocation.X + roamDirection.x * Chunk.TILE_WIDTH, currentLocation.Z + roamDirection.z * Chunk.TILE_WIDTH);
        WorldLocation targetLocation = new(target.x, target.z);

        if (target.x < 0 || target.z < 0 || target.x > WorldMap.WIDTH || target.z > WorldMap.WIDTH)
        {
            stepsTaken = 0;
            roamDirection = ChooseRoamDirection(currentLocation);
            target = (currentLocation.X + roamDirection.x * Chunk.TILE_WIDTH, currentLocation.Z + roamDirection.z * Chunk.TILE_WIDTH);
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
        for (int dist = 1; dist < VIEW_DISTANCE; ++dist)
        {
            float targetZ = currentLocation.Z + roamDirection.z * dist * Chunk.TILE_WIDTH;

            if (targetZ < 0 || targetZ > WorldMap.WIDTH)
                continue;

            for (int x = -dist; x <= dist + 1; ++x)
            {
                float targetX = currentLocation.X + x * Chunk.TILE_WIDTH;

                if (targetX < 0 || targetX > WorldMap.WIDTH)
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
        for (int dist = 1; dist < VIEW_DISTANCE; ++dist)
        {
            float targetX = currentLocation.X + roamDirection.x * dist * Chunk.TILE_WIDTH;

            if (targetX < 0 || targetX > WorldMap.WIDTH)
                continue;

            for (int z = -dist; z <= dist + 1; ++z)
            {
                float targetZ = currentLocation.Z + z * Chunk.TILE_WIDTH;

                if (targetZ < 0 || targetZ > WorldMap.WIDTH)
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
        for (int dist = 1; dist < VIEW_DISTANCE; ++dist)
        {
            for (int z = 0; z <= dist; ++z)
            {
                float targetZ = currentLocation.Z + roamDirection.z * z * Chunk.TILE_WIDTH;

                if (targetZ < 0 || targetZ > WorldMap.WIDTH)
                    continue;

                if (z == dist)
                {
                    for (int x = 0; x <= dist; ++x)
                    {
                        float targetX = currentLocation.X + roamDirection.x * x * Chunk.TILE_WIDTH;

                        if (targetX < 0 || targetX > WorldMap.WIDTH)
                            continue;

                        WorldLocation target = new(targetX, targetZ);

                        if (IsSpaceHouseOrFree(target, roamDirection))
                            return target;
                    }
                }
                else
                {
                    float targetX = currentLocation.X + roamDirection.x * dist * Chunk.TILE_WIDTH;

                    if (targetX < 0 || targetX > WorldMap.WIDTH)
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

        if (location.Z + Chunk.TILE_WIDTH <= WorldMap.WIDTH)
        {
            WorldLocation neighbor = new(location.X, location.Z + Chunk.TILE_WIDTH);

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
            WorldLocation vertex = new(start.X + x * Chunk.TILE_WIDTH, start.Z + z * Chunk.TILE_WIDTH);

            if (vertex.X < 0 || vertex.Z < 0 || vertex.X > WorldMap.WIDTH || vertex.Z > WorldMap.WIDTH ||
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