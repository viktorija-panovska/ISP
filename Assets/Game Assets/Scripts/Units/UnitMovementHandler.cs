using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public enum MoveState
{
    Free,
    FoundFriendlyHouse,
    FoundEnemyHouse,
    FoundFlatSpace,
    Stop
}


public class UnitMovementHandler : NetworkBehaviour
{
    private Unit Unit { get => GetComponent<Unit>(); }

    private const float MOVE_SPEED = 2f;
    private const float POSITION_ERROR = 0.5f;

    private bool isGuided = false;

    private readonly (int x, int z)[] directions = new (int, int)[] { (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1) };


    // Following path
    private List<WorldLocation> path;
    private int targetIndex = 0;
    private WorldLocation? pathTarget;
    private bool intermediateStep;

    public WorldLocation StartLocation { get; private set; }
    public WorldLocation EndLocation { get => pathTarget ?? StartLocation; }

    public Unit TargetUnit { get; private set; }


    // Roaming
    private const int VIEW_DISTANCE = 5;
    private const int ROAM_DISTANCE = 10;
    private int stepsTaken = 0;
    private (int x, int z) roamDirection = (0, 0);


    // Building
    private MoveState lastMoveState = MoveState.Free;
    private MoveState moveState = MoveState.Free;
    private List<WorldLocation> houseVertices;


    public void Initialize()
    {
        StartLocation = new WorldLocation(Unit.Position.x, Unit.Position.z);
        roamDirection = ChooseRoamDirection(new WorldLocation(Unit.Position.x, Unit.Position.z));
        ChooseNewRoamTarget(StartLocation);

        Resume();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (moveState == MoveState.Stop)
            return;

        if (path != null || pathTarget != null)
            FollowPath();
        else if (TargetUnit != null)
            FollowUnit();
        else
            Roam();
    }

    public void Stop()
    {
        lastMoveState = moveState;
        moveState = MoveState.Stop;

        Unit.PlayIdleAnimation();
    }

    public void Resume()
    {
        moveState = moveState != MoveState.Stop ? lastMoveState : MoveState.Free;

        Unit.PlayWalkAnimation();
    }



    #region Following path

    public WorldLocation? GetNextLocation() => pathTarget;

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
            if (path == null)
                return;

            pathTarget = path[targetIndex];
            Unit.Rotate(new Vector3(pathTarget.Value.X, 0, pathTarget.Value.Z) - new Vector3(Unit.Position.x, 0, Unit.Position.z));

            StartLocation = new(Unit.Position.x, Unit.Position.z, isCenter: intermediateStep);

            if (!intermediateStep && StartLocation.X != pathTarget.Value.X && StartLocation.Z != pathTarget.Value.Z)
            {
                intermediateStep = true;
                pathTarget = WorldLocation.GetCenter(StartLocation, pathTarget.Value);
            }
            else
                intermediateStep = false;
        }

        else if (!MoveToTarget(pathTarget.Value))   // we reached the target
        {
            pathTarget = null;

            if (path == null) return;

            if (!intermediateStep)
            {
                targetIndex++;

                if (targetIndex >= path.Count)
                {
                    EndPath();
                    return;
                }

                if (!isGuided)
                {
                    bool isGoalGone = moveState == MoveState.FoundFlatSpace && !IsStillFree() ||
                                      moveState == MoveState.FoundFriendlyHouse && !IsFriendlyHouse() ||
                                      moveState == MoveState.FoundEnemyHouse && !IsEnemyHouse();

                    List<WorldLocation> vertices = FindFreeSpaceOrHouse(Unit.Location);
                    if (vertices != null && houseVertices != null)
                    {
                        (WorldLocation location, float distance) newClosest = Helpers.GetClosestVertex(Unit.Position, vertices);
                        (WorldLocation l, float distance) oldClosest = Helpers.GetClosestVertex(Unit.Position, houseVertices);
                        if (isGoalGone || newClosest.distance < oldClosest.distance)
                        {
                            houseVertices = vertices;

                            if (moveState == MoveState.FoundEnemyHouse)
                            {
                                House house = (House)WorldMap.Instance.GetHouseAtVertex(houseVertices[0]);
                                SetPath(Pathfinding.FindPath(Unit.Location, Helpers.GetClosestVertex(Unit.Position, house.GetAttackableVertices()).location));
                            }
                            else
                            {
                                SetPath(Pathfinding.FindPath(Unit.Location, newClosest.location));
                            }
                        }
                    }
                    else if (isGoalGone)
                    {
                        path = null;
                        targetIndex = 0;
                        moveState = MoveState.Free;
                    }
                }
            }
        }
    }

    private bool MoveToTarget(WorldLocation target)
    {
        Vector3 targetPosition = new(target.X, WorldMap.Instance.GetHeight(target), target.Z);

        if (Mathf.Abs(Unit.Position.x - targetPosition.x) > POSITION_ERROR ||
            Mathf.Abs(Unit.Position.y - targetPosition.y) > POSITION_ERROR ||
            Mathf.Abs(Unit.Position.z - targetPosition.z) > POSITION_ERROR)
        {
            Unit.Position = Vector3.Lerp(Unit.Position, targetPosition, MOVE_SPEED * Time.deltaTime);
            return true;
        }
        return false;
    }

    #endregion


    #region Roaming

    private void Roam()
    {
        WorldLocation currentLocation = new(Unit.Position.x, Unit.Position.z);

        List<WorldLocation> vertices = null;
        
        if (Unit.UnitState == UnitStates.Settle)
            vertices = FindFreeSpaceOrHouse(currentLocation);

        if (vertices == null)
        {
            if (stepsTaken <= ROAM_DISTANCE && roamDirection != (0, 0))
                stepsTaken++;
            else
            {
                stepsTaken = 0;
                roamDirection = ChooseRoamDirection(currentLocation);
            }

            ChooseNewRoamTarget(currentLocation);
        }
        else
        {
            houseVertices = vertices;

            if (moveState == MoveState.FoundEnemyHouse)
            {
                House house = (House)WorldMap.Instance.GetHouseAtVertex(houseVertices[0]);
                SetPath(Pathfinding.FindPath(currentLocation, Helpers.GetClosestVertex(Unit.Position, house.GetAttackableVertices()).location));
            }
            else
            {
                SetPath(Pathfinding.FindPath(currentLocation, Helpers.GetClosestVertex(Unit.Position, houseVertices).location));
            }
        }
    }

    private (int, int) ChooseRoamDirection(WorldLocation currentLocation)
    {
        List<(int, int)> availableDirections = new();

        void AddValidDirections((int, int)[] d)
        {
            foreach ((int dx, int dz) in d)
            {
                WorldLocation location = new(currentLocation.X + dx * Chunk.TILE_WIDTH, currentLocation.Z + dz * Chunk.TILE_WIDTH);

                if (!location.IsInBounds() || !WorldMap.Instance.IsSpaceAccessible(location) || 
                    (WorldMap.Instance.IsOccupied(currentLocation) && WorldMap.Instance.IsOccupied(location) && Mathf.Abs(dx) == Mathf.Abs(dz)))
                    continue;

                availableDirections.Add((dx, dz));
            }
        }

        if (roamDirection == (0, 0))
            AddValidDirections(directions);
        else if (Unit.UnitState == UnitStates.Battle)
        {
            AddValidDirections(new (int, int)[] {
                directions[Helpers.NextArrayIndex(0, Unit.Team == Team.RED ? 1 : -1, directions.Length)],
                directions[Helpers.NextArrayIndex(0, Unit.Team == Team.RED ? 2 : -2, directions.Length)],
                directions[Helpers.NextArrayIndex(0, Unit.Team == Team.RED ? 3 : -3, directions.Length)],
                directions[0], directions[4] });

            if (availableDirections.Count == 0)
                AddValidDirections(new (int, int)[] {
                directions[Helpers.NextArrayIndex(0, Unit.Team == Team.RED ? -1 : 1, directions.Length)],
                directions[Helpers.NextArrayIndex(0, Unit.Team == Team.RED ? -2 : 2, directions.Length)],
                directions[Helpers.NextArrayIndex(0, Unit.Team == Team.RED ? -3 : 3, directions.Length)]});
        }
        else
        {
            int currentDirection = Array.IndexOf(directions, roamDirection);

            AddValidDirections(new (int, int)[] { 
                directions[Helpers.NextArrayIndex(currentDirection, -1, directions.Length)], 
                directions[Helpers.NextArrayIndex(currentDirection, +1, directions.Length)] });

            if (availableDirections.Count == 0)
                AddValidDirections(new (int, int)[] {
                directions[Helpers.NextArrayIndex(currentDirection, -2, directions.Length)],
                directions[Helpers.NextArrayIndex(currentDirection, +2, directions.Length)] });

            if (availableDirections.Count == 0)
                AddValidDirections(new (int, int)[] {
                directions[Helpers.NextArrayIndex(currentDirection, -3, directions.Length)],
                directions[Helpers.NextArrayIndex(currentDirection, +3, directions.Length)] });

            if (availableDirections.Count == 0)
                AddValidDirections(new (int, int)[] { directions[currentDirection],
                directions[Helpers.NextArrayIndex(currentDirection, +4, directions.Length)] });
        }

        return availableDirections.Count > 0 ? availableDirections[UnityEngine.Random.Range(0, availableDirections.Count - 1)] : (0, 0);
    }

    private void ChooseNewRoamTarget(WorldLocation currentLocation)
    {
        (float x, float z) target = (currentLocation.X + roamDirection.x * Chunk.TILE_WIDTH, currentLocation.Z + roamDirection.z * Chunk.TILE_WIDTH);
        WorldLocation targetLocation = new(target.x, target.z);

        if (!targetLocation.IsInBounds() || !WorldMap.Instance.IsSpaceAccessible(targetLocation) ||
            (WorldMap.Instance.IsOccupied(currentLocation) && WorldMap.Instance.IsOccupied(targetLocation) && Mathf.Abs(roamDirection.x) == Mathf.Abs(roamDirection.z)))
        {
            stepsTaken = 0;
            roamDirection = ChooseRoamDirection(currentLocation);
            target = (currentLocation.X + roamDirection.x * Chunk.TILE_WIDTH, currentLocation.Z + roamDirection.z * Chunk.TILE_WIDTH);
            targetLocation = new WorldLocation(target.x, target.z);
        }

        SetPath(new() { targetLocation });
    }


    private List<WorldLocation> FindFreeSpaceOrHouse(WorldLocation currentLocation)
    {
        List<WorldLocation> vertices = FindFreeSpaceOrHouse_Surrounding(currentLocation);
        if (vertices != null)
        {
            houseVertices = vertices;
            EndPath();
            return null;
        }

        if (roamDirection.x == 0)
            return FindFreeSpaceOrHouse_Vertical(currentLocation);
        else if (roamDirection.z == 0)
            return FindFreeSpaceOrHouse_Horizontal(currentLocation);
        else
            return FindFreeSpaceOrHouse_Diagonal(currentLocation);
    }

    private List<WorldLocation> FindFreeSpaceOrHouse_Vertical(WorldLocation currentLocation)
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
                if (WorldMap.Instance.IsSpaceAccessible(target) && !WorldMap.Instance.IsSpaceSwamp(target))
                {
                    List<WorldLocation> vertices = GetFreeSpaceOrHouse(target);
                    if (vertices != null)
                        return vertices;
                }
            }
        }

        return null;
    }

    private List<WorldLocation> FindFreeSpaceOrHouse_Horizontal(WorldLocation currentLocation)
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
                if (WorldMap.Instance.IsSpaceAccessible(target) && !WorldMap.Instance.IsSpaceSwamp(target))
                {
                    List<WorldLocation> vertices = GetFreeSpaceOrHouse(target);
                    if (vertices != null)
                        return vertices;
                }
            }
        }

        return null;
    }

    private List<WorldLocation> FindFreeSpaceOrHouse_Diagonal(WorldLocation currentLocation)
    {
        for (int dist = 1; dist < VIEW_DISTANCE; ++dist)
        {
            for (int z = 0; z <= dist; ++z)
            {
                (int, int)[] directions;

                if (z == dist)
                    directions = new (int, int)[] { (dist, dist) };
                else
                    directions = new (int, int)[] { (z, dist), (dist, z) };

                foreach ((int dx, int dz) in directions)
                {
                    WorldLocation target = new(currentLocation.X + roamDirection.x * dx * Chunk.TILE_WIDTH, currentLocation.Z + roamDirection.z * dz * Chunk.TILE_WIDTH);

                    if (target.IsInBounds() && WorldMap.Instance.IsSpaceAccessible(target) && !WorldMap.Instance.IsSpaceSwamp(target))
                    {
                        List<WorldLocation> vertices = GetFreeSpaceOrHouse(target);
                        if (vertices != null)
                            return vertices;
                    }
                }
            }
        }

        return null;
    }

    private List<WorldLocation> FindFreeSpaceOrHouse_Surrounding(WorldLocation location)
    {
        if (roamDirection.x == 0)
        {
            for (int z = 1; z >= -1; z -= 2)
            {
                for (int x = 1; x >= -1; x -= 2)
                {
                    List<WorldLocation> vertices = GetFreeSpaceOrHouse(location, new (int, int)[] { (0, z * roamDirection.z), (x, 0), (x, z * roamDirection.z) });
                    if (vertices != null) 
                        return vertices;
                }
            }

        }
        else if (roamDirection.z == 0)
        {
            for (int x = 1; x >= -1; x -= 2)
            {
                for (int z = 1; z >= -1; z -= 2)
                {
                    List<WorldLocation> vertices = GetFreeSpaceOrHouse(location, new (int, int)[] { (x * roamDirection.x, 0), (0, z), (x * roamDirection.x, z) });
                    if (vertices != null) 
                        return vertices;
                }
            }

        }
        else
        {
            for (int z = 1; z >= -1; z -= 2)
            {
                for (int x = 1; x >= -1; x -= 2)
                {
                    List<WorldLocation> vertices = GetFreeSpaceOrHouse(location, new (int, int)[] { (x * roamDirection.x, 0), (0, z * roamDirection.z), (x * roamDirection.x, z * roamDirection.z) });
                    if (vertices != null)
                        return vertices;
                }
            }
        }

        return null;
    }

    private List<WorldLocation> GetFreeSpaceOrHouse(WorldLocation start, (int, int)[] vertexOffsets = null)
    {
        List<WorldLocation> freeVertices = new();
        List<WorldLocation> occupiedVertices = new();
        bool isEnterable = false;

        if (WorldMap.Instance.IsOccupied(start))
        {
            occupiedVertices.Add(start);
            if (WorldMap.Instance.IsSpaceActiveHouse(start) && ((House)WorldMap.Instance.GetHouseAtVertex(start)).CanUnitEnter(Unit))
                isEnterable = true;
        }
        else
            freeVertices.Add(start);

        if (vertexOffsets == null)
        {
            if (roamDirection.x == 0)
                vertexOffsets = new[] { (0, roamDirection.z), (-1, 0), (-1, roamDirection.z) };
            else if (roamDirection.z == 0)
                vertexOffsets = new[] { (roamDirection.x, 0), (0, -1), (roamDirection.x, -1) };
            else
                vertexOffsets = new[] { (roamDirection.x, 0), (0, roamDirection.z), (roamDirection.x, roamDirection.z) };
        }

        foreach ((int x, int z) in vertexOffsets)
        {
            WorldLocation vertex = new(start.X + x * Chunk.TILE_WIDTH, start.Z + z * Chunk.TILE_WIDTH);

            if (vertex.Equals(start) || !vertex.IsInBounds() || WorldMap.Instance.GetHeight(start) != WorldMap.Instance.GetHeight(vertex) ||
                !WorldMap.Instance.IsSpaceAccessible(vertex) || WorldMap.Instance.IsSpaceSwamp(vertex))
                return null;

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
                return occupiedVertices;
            }
            else if (WorldMap.Instance.IsSpaceActiveHouse(start) && ((House)WorldMap.Instance.GetHouseAtVertex(start)).IsAttackable(Unit.Team))
            {
                moveState = MoveState.FoundEnemyHouse;
                return occupiedVertices;
            }
            else return null;
        }
        
        if (freeVertices.Count == 4)
        {
            moveState = MoveState.FoundFlatSpace;
            return freeVertices;
        }

        return null;
    }

    #endregion


    #region Following Unit

    public void SetFollowingUnit(Unit unit)
    {
        TargetUnit = unit;
        isGuided = true;
    }

    private void FollowUnit()
    {
        if (Unit.UnitState == UnitStates.Battle && TargetUnit.IsFighting)
            EndFollow();

        WorldLocation? path = Pathfinding.FollowUnit(Unit.Location, TargetUnit.Location);

        if (path != null)
            SetPath(new() { path.Value });
    }

    public void EndFollow()
    {
        TargetUnit = null;
        isGuided = false;
    }

    #endregion


    #region End of Path

    public void EndPath()
    {
        path = null;
        targetIndex = 0;

        if (moveState == MoveState.FoundFlatSpace && IsStillFree())
        {
            Stop();
            OldGameController.Instance.SpawnHouse(houseVertices, Unit.Team);
            Unit.EnterHouse();
            return;
        }
        else if (moveState == MoveState.FoundFriendlyHouse && IsFriendlyHouse())
        {
            Stop();
            Unit.EnterHouse();
            return;
        }
        else if (moveState == MoveState.FoundEnemyHouse && IsEnemyHouse())
        {
            Stop();
            Unit.AttackHouse();
            return;
        }
        else
            moveState = MoveState.Free;
    }

    private bool IsStillFree()
    {
        float height = WorldMap.Instance.GetHeight(houseVertices[0]);

        foreach (WorldLocation vertex in houseVertices)
            if (WorldMap.Instance.GetHeight(vertex) != height || WorldMap.Instance.IsOccupied(vertex))
                return false;

        return true;
    }

    private bool IsFriendlyHouse()
        => WorldMap.Instance.IsSpaceActiveHouse(houseVertices[0]) && 
           ((House)WorldMap.Instance.GetHouseAtVertex(houseVertices[0])).CanUnitEnter(Unit);

    private bool IsEnemyHouse()
        => WorldMap.Instance.IsSpaceActiveHouse(houseVertices[0]) && 
           ((House)WorldMap.Instance.GetHouseAtVertex(houseVertices[0])).IsAttackable(Unit.Team);

    #endregion
}